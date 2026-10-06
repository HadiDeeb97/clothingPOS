using ClothingStore.Core;
using ClothingStore.Core.Entities;
using Microsoft.EntityFrameworkCore;
using ClothingStore.Core.Localization;

namespace ClothingStore.Data.Services;

public sealed record NamedAmount(string Name, int Quantity, decimal Amount, int Count = 0);

public sealed record DailySales(DateTime Date, int Transactions, decimal Total);

public sealed record SalesReport
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public int Transactions { get; init; }
    public int ItemsSold { get; init; }
    public decimal GrossSales { get; init; }
    public decimal Discounts { get; init; }
    public decimal Tax { get; init; }

    /// <summary>Total collected from customers (tax included).</summary>
    public decimal TotalSales { get; init; }

    /// <summary>Sales excluding tax.</summary>
    public decimal NetSales { get; init; }

    public int ReturnsCount { get; init; }
    public decimal Refunds { get; init; }
    public decimal RefundTax { get; init; }

    /// <summary>Net sales excluding tax, after refunds.</summary>
    public decimal NetRevenue { get; init; }

    public decimal CostOfGoods { get; init; }
    public decimal GrossProfit => NetRevenue - CostOfGoods;
    public decimal MarginPercent => NetRevenue == 0 ? 0 : Math.Round(GrossProfit / NetRevenue * 100m, 1);
    public decimal AverageBasket => Transactions == 0 ? 0 : Money.Round(TotalSales / Transactions);
    public int VoidedCount { get; init; }

    public IReadOnlyList<NamedAmount> ByPaymentMethod { get; init; } = [];
    public IReadOnlyList<NamedAmount> ByCategory { get; init; } = [];
    public IReadOnlyList<NamedAmount> TopProducts { get; init; } = [];
    public IReadOnlyList<NamedAmount> ByCashier { get; init; } = [];
    public IReadOnlyList<NamedAmount> BySize { get; init; } = [];
    public IReadOnlyList<DailySales> ByDay { get; init; } = [];
}

public sealed record InventoryValuation(
    int Skus,
    int Units,
    decimal CostValue,
    decimal RetailValue,
    int LowStockCount,
    int OutOfStockCount,
    IReadOnlyList<NamedAmount> ByCategoryCost);

public class ReportService(IDbContextFactory<PosDbContext> factory)
{
    public async Task<SalesReport> GetSalesReportAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // Only the columns the report needs: whole sales with their products made long ranges slow.
        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.CreatedAt >= from && s.CreatedAt < to)
            .Select(s => new
            {
                s.Id, s.Status, s.CreatedAt, s.Subtotal, s.DiscountTotal, s.TaxTotal, s.Total,
                Cashier = s.User != null ? s.User.FullName : null,
            })
            .ToListAsync(ct);
        var completed = sales.Where(s => s.Status == SaleStatus.Completed).ToList();

        var lines = await db.SaleLines.AsNoTracking()
            .Where(l => l.Sale!.CreatedAt >= from && l.Sale.CreatedAt < to && l.Sale.Status == SaleStatus.Completed)
            .Select(l => new
            {
                l.SaleId, l.Quantity, l.LineTotal, l.TaxAmount, l.UnitCost, l.ProductName, l.CategoryName,
                Size = l.ProductVariant != null ? l.ProductVariant.Size : null,
            })
            .ToListAsync(ct);

        // SQL Server sums decimals exactly, so payment totals are grouped on the server.
        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.Sale!.CreatedAt >= from && p.Sale.CreatedAt < to && p.Sale.Status == SaleStatus.Completed)
            .GroupBy(p => p.Method)
            .Select(g => new { Method = g.Key, Amount = g.Sum(p => p.Amount), Count = g.Count() })
            .ToListAsync(ct);

        var returns = await db.Returns.AsNoTracking()
            .Where(r => r.CreatedAt >= from && r.CreatedAt < to)
            .Select(r => new { r.TotalRefund, r.TaxRefund })
            .ToListAsync(ct);
        var returnedCost = (await db.ReturnLines.AsNoTracking()
                .Where(rl => rl.SaleReturn!.CreatedAt >= from && rl.SaleReturn.CreatedAt < to && rl.Restocked)
                .Select(rl => new { rl.Quantity, UnitCost = rl.SaleLine != null ? rl.SaleLine.UnitCost : 0m })
                .ToListAsync(ct))
            .Sum(rl => rl.Quantity * rl.UnitCost);

        var itemsBySale = lines.GroupBy(l => l.SaleId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        var totalSales = completed.Sum(s => s.Total);
        var tax = completed.Sum(s => s.TaxTotal);
        var refunds = returns.Sum(r => r.TotalRefund);
        var refundTax = returns.Sum(r => r.TaxRefund);

        return new SalesReport
        {
            From = from,
            To = to,
            Transactions = completed.Count,
            VoidedCount = sales.Count - completed.Count,
            ItemsSold = lines.Sum(l => l.Quantity),
            GrossSales = completed.Sum(s => s.Subtotal),
            Discounts = completed.Sum(s => s.DiscountTotal),
            Tax = tax,
            TotalSales = totalSales,
            NetSales = totalSales - tax,
            ReturnsCount = returns.Count,
            Refunds = refunds,
            RefundTax = refundTax,
            NetRevenue = totalSales - tax - (refunds - refundTax),
            CostOfGoods = lines.Sum(l => l.UnitCost * l.Quantity) - returnedCost,

            ByPaymentMethod = payments
                .Select(p => new NamedAmount(Loc.EnumText(p.Method), 0, p.Amount, p.Count))
                .OrderByDescending(x => x.Amount).ToList(),

            ByCategory = lines
                .GroupBy(l => l.CategoryName ?? Loc.T("Reports.Uncategorised"))
                .Select(g => new NamedAmount(g.Key, g.Sum(l => l.Quantity), g.Sum(l => l.LineTotal - l.TaxAmount)))
                .OrderByDescending(x => x.Amount).ToList(),

            TopProducts = lines
                .GroupBy(l => l.ProductName)
                .Select(g => new NamedAmount(g.Key, g.Sum(l => l.Quantity), g.Sum(l => l.LineTotal - l.TaxAmount)))
                .OrderByDescending(x => x.Quantity).ThenByDescending(x => x.Amount)
                .Take(25).ToList(),

            BySize = lines
                .GroupBy(l => string.IsNullOrWhiteSpace(l.Size) ? "-" : l.Size)
                .Select(g => new NamedAmount(g.Key, g.Sum(l => l.Quantity), g.Sum(l => l.LineTotal - l.TaxAmount)))
                .OrderByDescending(x => x.Quantity).ToList(),

            ByCashier = completed
                .GroupBy(s => s.Cashier ?? "?")
                .Select(g => new NamedAmount(g.Key, g.Sum(s => itemsBySale.GetValueOrDefault(s.Id)), g.Sum(s => s.Total), g.Count()))
                .OrderByDescending(x => x.Amount).ToList(),

            ByDay = completed
                .GroupBy(s => s.CreatedAt.Date)
                .Select(g => new DailySales(g.Key, g.Count(), g.Sum(s => s.Total)))
                .OrderBy(d => d.Date).ToList(),
        };
    }

    public async Task<InventoryValuation> GetInventoryValuationAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var variants = await db.ProductVariants.AsNoTracking()
            .Include(v => v.Product).ThenInclude(p => p!.Category)
            .Where(v => v.IsActive && v.Product!.IsActive)
            .ToListAsync(ct);

        var inStock = variants.Where(v => v.StockQuantity > 0).ToList();
        return new InventoryValuation(
            Skus: variants.Count,
            Units: inStock.Sum(v => v.StockQuantity),
            CostValue: inStock.Sum(v => v.StockQuantity * v.EffectiveCost),
            RetailValue: inStock.Sum(v => v.StockQuantity * v.EffectivePrice),
            LowStockCount: variants.Count(v => v.IsLowStock && v.StockQuantity > 0),
            OutOfStockCount: variants.Count(v => v.StockQuantity <= 0),
            ByCategoryCost: inStock
                .GroupBy(v => v.Product!.Category?.Name ?? Loc.T("Reports.Uncategorised"))
                .Select(g => new NamedAmount(g.Key, g.Sum(v => v.StockQuantity), g.Sum(v => v.StockQuantity * v.EffectiveCost)))
                .OrderByDescending(x => x.Amount).ToList());
    }
}
