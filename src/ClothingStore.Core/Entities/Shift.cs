namespace ClothingStore.Core.Entities;

/// <summary>A cash-drawer session from opening float to end-of-day count (Z report).</summary>
public class Shift : Entity
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public DateTime OpenedAt { get; set; } = DateTime.Now;
    public DateTime? ClosedAt { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;

    public decimal OpeningFloat { get; set; }

    /// <summary>Calculated at close: float + cash sales + pay-ins - pay-outs - cash refunds (dollars).</summary>
    public decimal ExpectedCash { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? Variance => CountedCash is null ? null : CountedCash - ExpectedCash;

    /// <summary>Lebanese pounds in the drawer at opening.</summary>
    public decimal OpeningFloatLbp { get; set; }

    /// <summary>Same as <see cref="ExpectedCash"/> for the Lebanese pounds in the drawer.</summary>
    public decimal ExpectedCashLbp { get; set; }
    public decimal? CountedCashLbp { get; set; }
    public decimal? VarianceLbp => CountedCashLbp is null ? null : CountedCashLbp - ExpectedCashLbp;

    public string? Notes { get; set; }

    public List<CashMovement> CashMovements { get; set; } = [];
}

public class CashMovement : Entity
{
    public int ShiftId { get; set; }
    public Shift? Shift { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public CashMovementType Type { get; set; }

    /// <summary>In <see cref="Currency"/> (dollars or pounds).</summary>
    public decimal Amount { get; set; }
    public CashCurrency Currency { get; set; }
    public string Reason { get; set; } = "";
    public int UserId { get; set; }
}
