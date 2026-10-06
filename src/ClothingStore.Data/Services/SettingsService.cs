using ClothingStore.Core;
using ClothingStore.Core.Entities;
using Microsoft.EntityFrameworkCore;
using ClothingStore.Core.Localization;

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
        if (string.IsNullOrWhiteSpace(settings.StoreName)) throw new BusinessRuleException(Loc.T("Err.StoreNameRequired"));
        if (settings.TaxRate is < 0 or > 100) throw new BusinessRuleException(Loc.T("Err.TaxRateRange"));
        if (settings.MaxCashierDiscountPercent is < 0 or > 100) throw new BusinessRuleException(Loc.T("Err.DiscountLimitRange"));
        if (settings.LoyaltyPointsPerUnit < 0 || settings.LoyaltyPointValue < 0) throw new BusinessRuleException(Loc.T("Err.LoyaltyNegative"));
        if (settings.ReturnWindowDays < 0) throw new BusinessRuleException(Loc.T("Err.ReturnWindowNegative"));
        if (string.IsNullOrWhiteSpace(settings.ReceiptPrefix) || settings.ReceiptPrefix.Length > 5)
            throw new BusinessRuleException(Loc.T("Err.ReceiptPrefixLength"));
        settings.ReceiptWidth = Math.Clamp(settings.ReceiptWidth, 24, 80);
        settings.BackupFolder = QueryHelpers.Clean(settings.BackupFolder);
        settings.ReceiptLanguage = Loc.Normalize(settings.ReceiptLanguage);
        if (settings.AutoBackupIntervalHours is < 1 or > 168)
            throw new BusinessRuleException(Loc.T("Err.BackupInterval"));

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
