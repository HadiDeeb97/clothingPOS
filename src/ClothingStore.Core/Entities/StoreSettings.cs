namespace ClothingStore.Core.Entities;

/// <summary>Single-row table holding store-wide configuration.</summary>
public class StoreSettings : Entity
{
    public string StoreName { get; set; } = "My Clothing Store";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? TaxNumber { get; set; }

    /// <summary>Sales tax / VAT rate in percent (e.g. 15 = 15%).</summary>
    public decimal TaxRate { get; set; } = 15m;

    /// <summary>When true, shelf prices already include tax (typical for VAT regions).</summary>
    public bool PricesIncludeTax { get; set; } = true;

    public string CurrencySymbol { get; set; } = "$";
    public string ReceiptFooter { get; set; } = "Thank you for shopping with us!\nExchanges within 30 days with receipt.";
    public int ReceiptWidth { get; set; } = 42;

    /// <summary>Points earned per 1 currency unit paid (cash/card/wallet).</summary>
    public decimal LoyaltyPointsPerUnit { get; set; } = 1m;

    /// <summary>Currency value of a single loyalty point when redeemed.</summary>
    public decimal LoyaltyPointValue { get; set; } = 0.01m;

    /// <summary>Maximum discount a cashier may give without manager approval.</summary>
    public decimal MaxCashierDiscountPercent { get; set; } = 10m;

    public int ReturnWindowDays { get; set; } = 30;

    public string ReceiptPrefix { get; set; } = "R";

    /// <summary>Language printed on customer receipts ("en" or "ar"), independent of each cashier's screen language.</summary>
    public string ReceiptLanguage { get; set; } = "en";

    /// <summary>Allow selling an item the system thinks is out of stock (count was wrong).</summary>
    public bool AllowNegativeStock { get; set; }

    /// <summary>Folder on the SQL Server machine for backups; empty = the server's default backup folder.</summary>
    public string? BackupFolder { get; set; }

    /// <summary>Back up when a shift closes and whenever the last backup is older than <see cref="AutoBackupIntervalHours"/>.</summary>
    public bool AutoBackupEnabled { get; set; } = true;

    public int AutoBackupIntervalHours { get; set; } = 24;
}
