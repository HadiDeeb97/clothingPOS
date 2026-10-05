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

    /// <summary>Calculated at close: float + cash sales + pay-ins - pay-outs - cash refunds.</summary>
    public decimal ExpectedCash { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? Variance => CountedCash is null ? null : CountedCash - ExpectedCash;

    public string? Notes { get; set; }

    public List<CashMovement> CashMovements { get; set; } = [];
}

public class CashMovement : Entity
{
    public int ShiftId { get; set; }
    public Shift? Shift { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public CashMovementType Type { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    public int UserId { get; set; }
}
