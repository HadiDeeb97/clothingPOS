using ClothingStore.Core;
using ClothingStore.Core.Entities;

namespace ClothingStore.Data.Services;

/// <summary>Every stock change goes through here so the movement history always reconciles.</summary>
internal static class StockLedger
{
    public static StockMovement Apply(
        PosDbContext db,
        ProductVariant variant,
        int change,
        StockMovementType type,
        int? userId,
        string? reference = null,
        string? notes = null,
        bool allowNegative = false)
    {
        var after = variant.StockQuantity + change;
        if (after < 0 && !allowNegative)
            throw new BusinessRuleException(
                $"Not enough stock for {variant.Sku}: {variant.StockQuantity} available, {-change} requested.");

        variant.StockQuantity = after;
        var movement = new StockMovement
        {
            ProductVariant = variant,
            ProductVariantId = variant.Id,
            Type = type,
            QuantityChange = change,
            QuantityAfter = after,
            UserId = userId,
            Reference = reference,
            Notes = notes,
            CreatedAt = DateTime.Now,
        };
        db.StockMovements.Add(movement);
        return movement;
    }
}
