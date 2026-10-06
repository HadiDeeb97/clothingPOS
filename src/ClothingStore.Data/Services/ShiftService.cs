using ClothingStore.Core;
using ClothingStore.Core.Entities;
using Microsoft.EntityFrameworkCore;
using ClothingStore.Core.Localization;

namespace ClothingStore.Data.Services;

public sealed record ShiftSummary
{
    public required int ShiftId { get; init; }
    public required string Cashier { get; init; }
    public required DateTime OpenedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
    public decimal OpeningFloat { get; init; }
    public int SalesCount { get; init; }
    public int VoidedCount { get; init; }
    public int ItemsSold { get; init; }
    public decimal GrossSales { get; init; }
    public decimal Discounts { get; init; }
    public decimal Tax { get; init; }
    public decimal TotalSales { get; init; }
    public IReadOnlyDictionary<PaymentMethod, decimal> Payments { get; init; } = new Dictionary<PaymentMethod, decimal>();
    public int ReturnsCount { get; init; }
    public IReadOnlyDictionary<RefundMethod, decimal> Refunds { get; init; } = new Dictionary<RefundMethod, decimal>();
    public decimal TotalRefunds { get; init; }
    public decimal PayIns { get; init; }
    public decimal PayOuts { get; init; }
    public decimal ExpectedCash { get; init; }
    public decimal? CountedCash { get; init; }
    public decimal? Variance => CountedCash - ExpectedCash;
    public IReadOnlyList<CashMovement> CashMovements { get; init; } = [];

    public decimal CashSales => Payments.GetValueOrDefault(PaymentMethod.Cash);
    public decimal CashRefunds => Refunds.GetValueOrDefault(RefundMethod.Cash);
}

public class ShiftService(IDbContextFactory<PosDbContext> factory)
{
    public async Task<Shift?> GetOpenShiftAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Shifts.AsNoTracking().Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == ShiftStatus.Open, ct);
    }

    public async Task<Shift> OpenShiftAsync(int userId, decimal openingFloat, CancellationToken ct = default)
    {
        if (openingFloat < 0) throw new BusinessRuleException(Loc.T("Err.FloatNegative"));
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.Shifts.AnyAsync(s => s.UserId == userId && s.Status == ShiftStatus.Open, ct))
            throw new BusinessRuleException(Loc.T("Err.ShiftAlreadyOpen"));

        var shift = new Shift { UserId = userId, OpeningFloat = Money.Round(openingFloat), OpenedAt = DateTime.Now, Status = ShiftStatus.Open };
        db.Shifts.Add(shift);
        await db.SaveChangesAsync(ct);
        return shift;
    }

    public async Task<CashMovement> AddCashMovementAsync(int shiftId, CashMovementType type, decimal amount, string reason, int userId, CancellationToken ct = default)
    {
        amount = Money.Round(amount);
        if (amount <= 0) throw new BusinessRuleException(Loc.T("Err.AmountPositive"));
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleException(Loc.T("Err.EnterReason"));

        await using var db = await factory.CreateDbContextAsync(ct);
        var shift = await db.Shifts.FindAsync([shiftId], ct) ?? throw new BusinessRuleException(Loc.T("Err.ShiftNotFound"));
        if (shift.Status != ShiftStatus.Open) throw new BusinessRuleException(Loc.T("Err.ShiftClosed"));

        var movement = new CashMovement { ShiftId = shiftId, Type = type, Amount = amount, Reason = reason.Trim(), UserId = userId, CreatedAt = DateTime.Now };
        db.CashMovements.Add(movement);
        await db.SaveChangesAsync(ct);
        return movement;
    }

    public async Task<ShiftSummary> GetSummaryAsync(int shiftId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await BuildSummaryAsync(db, shiftId, ct);
    }

    public async Task<ShiftSummary> CloseShiftAsync(int shiftId, decimal countedCash, string? notes, CancellationToken ct = default)
    {
        if (countedCash < 0) throw new BusinessRuleException(Loc.T("Err.CountedCashNegative"));
        await using var db = await factory.CreateDbContextAsync(ct);
        var shift = await db.Shifts.FindAsync([shiftId], ct) ?? throw new BusinessRuleException(Loc.T("Err.ShiftNotFound"));
        if (shift.Status != ShiftStatus.Open) throw new BusinessRuleException(Loc.T("Err.ShiftAlreadyClosed"));

        var summary = await BuildSummaryAsync(db, shiftId, ct);
        shift.ExpectedCash = summary.ExpectedCash;
        shift.CountedCash = Money.Round(countedCash);
        shift.Notes = QueryHelpers.Clean(notes);
        shift.ClosedAt = DateTime.Now;
        shift.Status = ShiftStatus.Closed;
        await db.SaveChangesAsync(ct);

        return await BuildSummaryAsync(db, shiftId, ct);
    }

    public async Task<List<Shift>> GetShiftsAsync(DateTime from, DateTime to, int? userId = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Shifts.AsNoTracking().Include(s => s.User)
            .Where(s => s.OpenedAt >= from && s.OpenedAt < to);
        if (userId is not null) query = query.Where(s => s.UserId == userId);
        return await query.OrderByDescending(s => s.OpenedAt).ToListAsync(ct);
    }

    private static async Task<ShiftSummary> BuildSummaryAsync(PosDbContext db, int shiftId, CancellationToken ct)
    {
        var shift = await db.Shifts.AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.CashMovements)
            .FirstOrDefaultAsync(s => s.Id == shiftId, ct)
            ?? throw new BusinessRuleException(Loc.T("Err.ShiftNotFound"));

        var sales = await db.Sales.AsNoTracking()
            .Include(s => s.Lines)
            .Include(s => s.Payments)
            .AsSplitQuery()
            .Where(s => s.ShiftId == shiftId)
            .ToListAsync(ct);
        var completed = sales.Where(s => s.Status == SaleStatus.Completed).ToList();

        var returns = await db.Returns.AsNoTracking().Include(r => r.Refunds).Where(r => r.ShiftId == shiftId).ToListAsync(ct);

        var payments = completed.SelectMany(s => s.Payments)
            .GroupBy(p => p.Method)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
        var refunds = returns.SelectMany(r => r.Refunds).GroupBy(r => r.Method).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));

        var payIns = shift.CashMovements.Where(m => m.Type == CashMovementType.PayIn).Sum(m => m.Amount);
        var payOuts = shift.CashMovements.Where(m => m.Type == CashMovementType.PayOut).Sum(m => m.Amount);
        var expected = shift.OpeningFloat
                       + payments.GetValueOrDefault(PaymentMethod.Cash)
                       + payIns - payOuts
                       - refunds.GetValueOrDefault(RefundMethod.Cash);

        return new ShiftSummary
        {
            ShiftId = shift.Id,
            Cashier = shift.User?.FullName ?? "",
            OpenedAt = shift.OpenedAt,
            ClosedAt = shift.ClosedAt,
            OpeningFloat = shift.OpeningFloat,
            SalesCount = completed.Count,
            VoidedCount = sales.Count - completed.Count,
            ItemsSold = completed.Sum(s => s.Lines.Sum(l => l.Quantity)),
            GrossSales = completed.Sum(s => s.Subtotal),
            Discounts = completed.Sum(s => s.DiscountTotal),
            Tax = completed.Sum(s => s.TaxTotal),
            TotalSales = completed.Sum(s => s.Total),
            Payments = payments,
            ReturnsCount = returns.Count,
            Refunds = refunds,
            TotalRefunds = returns.Sum(r => r.TotalRefund),
            PayIns = payIns,
            PayOuts = payOuts,
            ExpectedCash = shift.Status == ShiftStatus.Closed ? shift.ExpectedCash : expected,
            CountedCash = shift.CountedCash,
            CashMovements = shift.CashMovements.OrderBy(m => m.CreatedAt).ToList(),
        };
    }
}
