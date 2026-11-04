using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Data.OnlineReports;

namespace ClothingStore.Tests;

/// <summary>Reorder levels: what new items start with, and "reorder level 0 never shows as low on stock".</summary>
public class ReorderLevelTests
{
    [Theory]
    [InlineData(0, 0, false, true)]   // out of stock, level 0: low unless the shop chose otherwise
    [InlineData(0, 0, true, false)]
    [InlineData(-2, 0, true, false)]  // sold below zero: still never low with the option
    [InlineData(5, 0, false, false)]
    [InlineData(2, 2, true, true)]    // levels of 1 or more behave as always
    [InlineData(3, 2, true, false)]
    public void When_an_item_is_low_on_stock(int stock, int level, bool zeroNeverLow, bool low) =>
        Assert.Equal(low, StockRules.IsLow(stock, level, zeroNeverLow));

    /// <summary>One product: S out of stock at level 0, M at its level 2, L plenty at level 0.</summary>
    private static async Task<Product> ShelfAsync(TestDatabase db)
    {
        var category = await db.Categories.SaveAsync(new Category { Name = "Tops" });
        return await db.Products.SaveAsync(new Product
        {
            Name = "Shelf Tee", CategoryId = category.Id, Price = 10, Cost = 4,
            Variants =
            [
                new ProductVariant { Size = "S", ReorderLevel = 0, StockQuantity = 0 },
                new ProductVariant { Size = "M", ReorderLevel = 2, StockQuantity = 2 },
                new ProductVariant { Size = "L", ReorderLevel = 0, StockQuantity = 5 },
            ],
        }, db.Admin.Id);
    }

    private static async Task SetZeroNeverLowAsync(TestDatabase db, bool value)
    {
        var settings = (await db.Settings.GetAsync()).Clone();
        settings.ZeroReorderNeverLow = value;
        await db.Settings.SaveAsync(settings);
    }

    [Theory]
    [InlineData(false, new[] { "S", "M" })]
    [InlineData(true, new[] { "M" })]
    public async Task Every_low_stock_list_follows_the_option(bool zeroNeverLow, string[] lowSizes)
    {
        await using var db = await TestDatabase.CreateAsync();
        await ShelfAsync(db);
        await SetZeroNeverLowAsync(db, zeroNeverLow);

        // Inventory's "Low stock only" (also what purchase orders add as low-stock items)
        Assert.Equal(lowSizes.Order(), (await db.Inventory.GetStockAsync(lowStockOnly: true)).Select(v => v.Size).Order());

        // Reports: the low-stock list
        var alerts = await db.Reports.GetStockAlertsAsync(DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1));
        Assert.Equal(lowSizes.Length, alerts.LowStock.Count);

        // The online reports website: the count on the live page and the stock page's list
        var builder = new OnlineReportsBuilder(db.Factory, db.Reports, db.Shifts);
        Assert.Equal(lowSizes.Length, (await builder.LiveAsync()).LowStockCount);
        var stock = await builder.StockAsync(DateTime.Today);
        Assert.Equal(lowSizes.Length, stock.LowStockCount);
        Assert.Equal(lowSizes.Length, stock.LowStock.Count);

        // Valuation counts items low but not yet out (M either way: S is out of stock)
        Assert.Equal(1, (await db.Reports.GetInventoryValuationAsync()).LowStockCount);
    }

    [Fact]
    public async Task The_default_level_is_kept_and_checked()
    {
        await using var db = await TestDatabase.CreateAsync();
        Assert.Equal(2, (await db.Settings.GetAsync()).DefaultReorderLevel); // as before it could be changed

        var settings = (await db.Settings.GetAsync()).Clone();
        settings.DefaultReorderLevel = 5;
        await db.Settings.SaveAsync(settings);
        Assert.Equal(5, (await db.Settings.GetAsync()).DefaultReorderLevel);

        var wrong = (await db.Settings.GetAsync()).Clone();
        wrong.DefaultReorderLevel = -1;
        await Assert.ThrowsAsync<BusinessRuleException>(() => db.Settings.SaveAsync(wrong));
    }

    [Fact]
    public async Task A_level_set_on_an_item_is_saved_as_given_including_zero()
    {
        await using var db = await TestDatabase.CreateAsync();
        var product = await ShelfAsync(db);
        var small = product.Variants.Single(v => v.Size == "S");

        await db.Inventory.UpdateReorderLevelAsync(small.Id, 0);
        Assert.Equal(0, (await db.GetVariantAsync(small.Id)).ReorderLevel);
        await db.Inventory.UpdateReorderLevelAsync(small.Id, 7);
        Assert.Equal(7, (await db.GetVariantAsync(small.Id)).ReorderLevel);
    }
}
