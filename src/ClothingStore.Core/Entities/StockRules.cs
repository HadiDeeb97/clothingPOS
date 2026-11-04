namespace ClothingStore.Core.Entities;

/// <summary>When an item counts as low on stock.</summary>
public static class StockRules
{
    /// <summary>
    /// The shop's "items with reorder level 0 never show as low on stock" setting, for screens that ask an item directly
    /// (<see cref="ProductVariant.IsLowStock"/>). Kept up to date by the app from the settings; services pass it themselves.
    /// </summary>
    public static bool ZeroLevelNeverLow { get; set; }

    /// <summary>At or below its reorder level, unless the level is 0 and the shop chose that 0 means "never low".</summary>
    public static bool IsLow(int stock, int reorderLevel, bool zeroLevelNeverLow) =>
        !(zeroLevelNeverLow && reorderLevel <= 0) && stock <= reorderLevel;
}
