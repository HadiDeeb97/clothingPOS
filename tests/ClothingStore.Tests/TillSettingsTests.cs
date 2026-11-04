using ClothingStore.Data.Services;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Tests;

/// <summary>Each till's own settings (printers, cash drawer, screen...) kept in the shop database.</summary>
public class TillSettingsTests
{
    [Fact]
    public async Task A_tills_settings_come_back_as_saved_and_are_replaced_by_newer_ones()
    {
        await using var db = await TestDatabase.CreateAsync();
        var tills = new TillSettingsService(db.Factory);

        Assert.Null(await tills.GetAsync("PC-1")); // nothing saved yet: the till's own copy is sent

        await tills.SaveAsync("PC-1", "COUNTER", """{"ReceiptPrinter":"XP-80C","CashDrawer":true}""");
        Assert.Equal("""{"ReceiptPrinter":"XP-80C","CashDrawer":true}""", await tills.GetAsync("PC-1"));

        await tills.SaveAsync("PC-1", "COUNTER", """{"ReceiptPrinter":"EPSON TM-T20","CashDrawer":true}""");
        Assert.Equal("""{"ReceiptPrinter":"EPSON TM-T20","CashDrawer":true}""", await tills.GetAsync("PC-1"));
        Assert.Equal(1, await Count(db));
    }

    [Fact]
    public async Task Each_till_keeps_its_own_settings()
    {
        await using var db = await TestDatabase.CreateAsync();
        var tills = new TillSettingsService(db.Factory);

        await tills.SaveAsync("PC-1", "COUNTER", """{"ReceiptPrinter":"XP-80C"}""");
        await tills.SaveAsync("PC-2", "BACK OFFICE", """{"ReceiptPrinter":"Office laser"}""");

        Assert.Equal("""{"ReceiptPrinter":"XP-80C"}""", await tills.GetAsync("PC-1"));
        Assert.Equal("""{"ReceiptPrinter":"Office laser"}""", await tills.GetAsync("PC-2"));
        Assert.Null(await tills.GetAsync("PC-3"));
    }

    [Fact]
    public async Task Saving_from_two_places_at_once_keeps_one_copy_and_never_fails()
    {
        await using var db = await TestDatabase.CreateAsync();
        var tills = new TillSettingsService(db.Factory);

        // Two Windows accounts on one till, or a quick change right after start: the first saves race each other.
        await Task.WhenAll(Enumerable.Range(0, 8).Select(i => tills.SaveAsync("PC-1", "COUNTER", $$"""{"ReceiptCopies":{{i}}}""")));

        Assert.Equal(1, await Count(db, "PC-1"));
        Assert.StartsWith("""{"ReceiptCopies":""", await tills.GetAsync("PC-1"));
    }

    [Fact]
    public async Task Large_settings_fit()
    {
        await using var db = await TestDatabase.CreateAsync();
        var tills = new TillSettingsService(db.Factory);
        // Hundreds of remembered panel and column sizes.
        var json = "{\"Layout\":{" + string.Join(",", Enumerable.Range(0, 500).Select(i => $"\"cols:Table{i}\":\"120:{i};-:{i + 1}\"")) + "}}";

        await tills.SaveAsync("PC-1", null, json);

        Assert.Equal(json, await tills.GetAsync("PC-1"));
    }

    private static async Task<int> Count(TestDatabase db, string? tillId = null)
    {
        await using var ctx = await db.Factory.CreateDbContextAsync();
        return await ctx.TillSettings.CountAsync(t => tillId == null || t.TillId == tillId);
    }
}
