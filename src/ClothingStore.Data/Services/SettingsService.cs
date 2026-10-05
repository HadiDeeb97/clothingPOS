using ClothingStore.Core;
using ClothingStore.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Data.Services;

public class SettingsService(IDbContextFactory<PosDbContext> factory)
{
    private StoreSettings? _cached;

    /// <summary>Raised after settings are saved so open screens can refresh (tax rate, currency...).</summary>
    public event EventHandler? SettingsChanged;

    public StoreSettings Current => _cached ?? throw new InvalidOperationException("Settings not loaded yet.");

    public async Task<StoreSettings> GetAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        _cached = await db.Settings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct) ?? new StoreSettings();
        return _cached;
    }

    public async Task SaveAsync(StoreSettings settings, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.StoreName)) throw new BusinessRuleException("Store name is required.");
        if (settings.TaxRate is < 0 or > 100) throw new BusinessRuleException("Tax rate must be between 0 and 100.");
        if (settings.MaxCashierDiscountPercent is < 0 or > 100) throw new BusinessRuleException("Discount limit must be between 0 and 100.");
        if (settings.LoyaltyPointsPerUnit < 0 || settings.LoyaltyPointValue < 0) throw new BusinessRuleException("Loyalty values cannot be negative.");
        if (settings.ReturnWindowDays < 0) throw new BusinessRuleException("Return window cannot be negative.");
        if (string.IsNullOrWhiteSpace(settings.ReceiptPrefix) || settings.ReceiptPrefix.Length > 5)
            throw new BusinessRuleException("Receipt prefix must be 1-5 characters.");
        settings.ReceiptWidth = Math.Clamp(settings.ReceiptWidth, 24, 80);

        await using var db = await factory.CreateDbContextAsync(ct);
        var existing = await db.Settings.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            settings.Id = 0;
            db.Settings.Add(settings);
        }
        else
        {
            settings.Id = existing.Id;
            db.Entry(existing).CurrentValues.SetValues(settings);
        }
        await db.SaveChangesAsync(ct);
        _cached = settings;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
