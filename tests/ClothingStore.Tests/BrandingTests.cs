using ClothingStore.Core;

namespace ClothingStore.Tests;

public class BrandingTests
{
    [Fact]
    public async Task Managers_can_set_and_remove_the_logo_but_cashiers_cannot()
    {
        await using var db = await TestDatabase.CreateAsync();
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };

        Assert.Null(await db.Branding.GetLogoAsync());
        await Assert.ThrowsAsync<BusinessRuleException>(() => db.Branding.SetLogoAsync(png, db.Cashier.Id));

        var version = await db.Branding.SetLogoAsync(png, db.Manager.Id);
        var logo = await db.Branding.GetLogoAsync();
        Assert.Equal(png, logo!.Image);
        Assert.Equal(db.Manager.Id, logo.UpdatedByUserId);
        Assert.Equal(version, await db.Branding.GetLogoVersionAsync());

        // Replacing keeps a single row.
        await db.Branding.SetLogoAsync([1, 2], db.Admin.Id);
        Assert.Equal(new byte[] { 1, 2 }, (await db.Branding.GetLogoAsync())!.Image);

        Assert.Null(await db.Branding.SetLogoAsync(null, db.Admin.Id));
        Assert.Null(await db.Branding.GetLogoAsync());
        Assert.Null(await db.Branding.GetLogoVersionAsync());
    }

    [Fact]
    public async Task Oversized_images_are_refused()
    {
        await using var db = await TestDatabase.CreateAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            db.Branding.SetLogoAsync(new byte[ClothingStore.Data.Services.StoreLogoLimits.MaxBytes + 1], db.Admin.Id));
    }
}
