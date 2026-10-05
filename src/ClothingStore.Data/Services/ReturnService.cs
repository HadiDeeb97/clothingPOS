using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Data.Services;

/// <summary>
/// Refunds items from a previous sale. Exchanges are handled as a return to store credit
/// followed by a new sale paid with that credit.
/// </summary>
public class ReturnService(IDbContextFactory<PosDbContext> factory)
{
    public async Task<SaleReturn> ProcessReturnAsync(ReturnRequest request, CancellationToken ct = default)
    {
        var requested = request.Lines.Where(l => l.Quantity > 0).ToList();
        if (requested.Count == 0) throw new BusinessRuleException("Choose at least one item to return.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var settings = await db.Settings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct) ?? new StoreSettings();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive, ct)
                   ?? throw new BusinessRuleException("User not found.");

        var sale = await db.Sales
            .Include(s => s.Lines).ThenInclude(l => l.ProductVariant)
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.Id == request.SaleId, ct)
            ?? throw new BusinessRuleException("Sale not found.");

        if (sale.Status != SaleStatus.Completed) throw new BusinessRuleException("Voided sales cannot be returned.");

        var age = DateTime.Now - sale.CreatedAt;
        if (settings.ReturnWindowDays > 0 && age.TotalDays > settings.ReturnWindowDays)
        {
            var approved = Permissions.Has(user.Role, Permission.OverrideDiscountLimit);
            if (!approved && request.ApprovedByUserId is { } approverId)
            {
                var approver = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == approverId && u.IsActive, ct);
                approved = approver is not null && Permissions.Has(approver.Role, Permission.OverrideDiscountLimit);
            }
            if (!approved)
                throw new BusinessRuleException($"This sale is older than the {settings.ReturnWindowDays}-day return window. Manager approval required.");
        }

        if (request.RefundMethod == RefundMethod.StoreCredit && sale.Customer is null)
            throw new BusinessRuleException("Store credit refunds need a customer on the original sale.");

        Shift? shift = null;
        if (request.ShiftId is { } shiftId)
        {
            shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId, ct);
            if (shift is not { Status: ShiftStatus.Open }) throw new BusinessRuleException("The shift is closed.");
        }
        if (request.RefundMethod == RefundMethod.Cash && shift is null)
            throw new BusinessRuleException("Open a cash drawer shift to give cash refunds.");

        var previousReturns = (await db.ReturnLines
                .Where(rl => rl.SaleLine!.SaleId == sale.Id)
                .Select(rl => new { rl.SaleLineId, rl.RefundAmount, rl.TaxAmount })
                .ToListAsync(ct))
            .GroupBy(x => x.SaleLineId)
            .ToDictionary(g => g.Key, g => (Refund: g.Sum(x => x.RefundAmount), Tax: g.Sum(x => x.TaxAmount)));

        var now = DateTime.Now;
        var ret = new SaleReturn
        {
            CreatedAt = now,
            SaleId = sale.Id,
            UserId = user.Id,
            CustomerId = sale.CustomerId,
            ShiftId = shift?.Id,
            RefundMethod = request.RefundMethod,
            Reason = QueryHelpers.Clean(request.Reason),
            ReturnNumber = await NextReturnNumberAsync(db, now, ct),
        };

        foreach (var req in requested)
        {
            var line = sale.Lines.FirstOrDefault(l => l.Id == req.SaleLineId)
                       ?? throw new BusinessRuleException("Item does not belong to this sale.");
            if (req.Quantity > line.ReturnableQuantity)
                throw new BusinessRuleException($"Only {line.ReturnableQuantity} x {line.ProductName} can still be returned.");

            decimal refund, tax;
            if (req.Quantity == line.ReturnableQuantity)
            {
                // Last units: refund whatever is left so the line reconciles to the cent.
                previousReturns.TryGetValue(line.Id, out var prev);
                refund = line.LineTotal - prev.Refund;
                tax = line.TaxAmount - prev.Tax;
            }
            else
            {
                refund = Money.Round(line.LineTotal * req.Quantity / line.Quantity);
                tax = Money.Round(line.TaxAmount * req.Quantity / line.Quantity);
            }

            line.ReturnedQuantity += req.Quantity;
            ret.Lines.Add(new SaleReturnLine
            {
                SaleLineId = line.Id,
                Quantity = req.Quantity,
                RefundAmount = refund,
                TaxAmount = tax,
                Restocked = req.Restock,
            });
            ret.TotalRefund += refund;
            ret.TaxRefund += tax;

            if (req.Restock)
                StockLedger.Apply(db, line.ProductVariant!, req.Quantity, StockMovementType.Return, user.Id, ret.ReturnNumber, QueryHelpers.Clean(request.Reason));
        }

        if (sale.Customer is { } customer)
        {
            if (request.RefundMethod == RefundMethod.StoreCredit) customer.StoreCredit += ret.TotalRefund;
            var pointsToRemove = (int)Math.Floor(ret.TotalRefund * settings.LoyaltyPointsPerUnit);
            customer.LoyaltyPoints = Math.Max(0, customer.LoyaltyPoints - Math.Min(pointsToRemove, sale.LoyaltyPointsEarned));
        }

        db.Returns.Add(ret);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("The item or customer was updated at another till. Please try again.");
        }
        return ret;
    }

    public async Task<List<SaleReturn>> SearchAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Returns.AsNoTracking()
            .Include(r => r.Sale)
            .Include(r => r.User)
            .Include(r => r.Customer)
            .Include(r => r.Lines).ThenInclude(l => l.SaleLine)
            .AsSplitQuery()
            .Where(r => r.CreatedAt >= from && r.CreatedAt < to)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<SaleReturn?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Returns.AsNoTracking()
            .Include(r => r.Sale)
            .Include(r => r.User)
            .Include(r => r.Customer)
            .Include(r => r.Lines).ThenInclude(l => l.SaleLine)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    private static async Task<string> NextReturnNumberAsync(PosDbContext db, DateTime date, CancellationToken ct)
    {
        const string prefix = "RT";
        var stem = QueryHelpers.DayStem(prefix, date);
        var last = await db.Returns
            .Where(r => r.ReturnNumber.StartsWith(stem))
            .OrderByDescending(r => r.ReturnNumber)
            .Select(r => r.ReturnNumber)
            .FirstOrDefaultAsync(ct);
        return QueryHelpers.NextNumber(prefix, date, last);
    }
}
