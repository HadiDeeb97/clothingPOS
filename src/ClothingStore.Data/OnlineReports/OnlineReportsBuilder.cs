using System.Security.Cryptography;
using ClothingStore.Core;
using ClothingStore.Core.Localization;
using ClothingStore.Data.Services;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Data.OnlineReports;

/// <summary>
/// Turns the POS's reports into the documents the website shows. The figures come from <see cref="ReportService"/> (the
/// Reports page's own code), so the website and the POS always agree.
/// </summary>
public class OnlineReportsBuilder(IDbContextFactory<PosDbContext> factory, ReportService reports, ShiftService shifts)
{
    public const int SlowSinceDays = 60;
    private const int TopProducts = 20;
    private const int TableRows = 50;
    private const int StockRows = 200;
    private const int RecentSales = 20;
    private const int RecentCloses = 20;

    /// <summary>The standard periods, as on the Reports page's quick buttons (weeks start on Monday).</summary>
    public static readonly IReadOnlyList<string> Periods = ["today", "yesterday", "week", "lastweek", "month", "lastmonth", "year"];

    /// <summary>First day and the day after the last of a standard period.</summary>
    public static (DateTime From, DateTime To) PeriodRange(string period, DateTime today)
    {
        today = today.Date;
        var monday = today.AddDays(-((7 + (int)today.DayOfWeek - 1) % 7));
        var month = new DateTime(today.Year, today.Month, 1);
        return period switch
        {
            "today" => (today, today.AddDays(1)),
            "yesterday" => (today.AddDays(-1), today),
            "week" => (monday, today.AddDays(1)),
            "lastweek" => (monday.AddDays(-7), monday),
            "month" => (month, today.AddDays(1)),
            "lastmonth" => (month.AddMonths(-1), month),
            "year" => (new DateTime(today.Year, 1, 1), today.AddDays(1)),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, null),
        };
    }

    public async Task<StoreDoc> StoreAsync(CancellationToken ct = default)
    {
        // Read here rather than through SettingsService, whose copy the screens share: this runs in the background.
        await using var db = await factory.CreateDbContextAsync(ct);
        var s = await db.Settings.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct) ?? new Core.Entities.StoreSettings();
        return new StoreDoc(s.StoreName, s.CurrencySymbol, s.LbpEnabled, s.LbpRate, Loc.Normalize(s.ReceiptLanguage));
    }

    /// <summary>The report from <paramref name="from"/> up to (not including) <paramref name="to"/>.</summary>
    public async Task<ReportDoc> ReportAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var r = await reports.GetSalesReportAsync(from, to, ct);
        return new ReportDoc
        {
            From = Day(from),
            To = Day(to.AddDays(-1)),
            Totals = new ReportTotals(
                r.TotalSales, r.GrossSales, r.Discounts, r.Tax, r.NetRevenue, r.CostOfGoods, r.GrossProfit, r.MarginPercent,
                r.Transactions, r.ItemsSold, r.AverageBasket, r.Refunds, r.ReturnsCount, r.VoidedCount, r.DeliveryFees),
            Change = new ReportChange(r.SalesChangePercent, r.TransactionsChangePercent, r.ProfitChangePercent),
            ByDay = r.ByDay.Select(d => new DayPoint(Day(d.Date), d.Transactions, d.Total)).ToList(),
            ByHour = r.ByHour.Select(h => new HourPoint(h.Hour, h.Transactions, h.Total)).ToList(),
            Payments = Rows(r.ByPaymentMethod),
            Categories = Rows(r.ByCategory),
            Products = Rows(r.TopProducts.Take(TopProducts)),
            Cashiers = Rows(r.ByCashier),
            Sizes = Rows(r.BySize),
            Channels = Rows(r.ByChannel),
            ReturnsByReason = Rows(r.ReturnsByReason.Take(TableRows)),
            TopReturned = Rows(r.TopReturned.Take(TableRows)),
            RefundsByMethod = Rows(r.RefundsByMethod),
            Settlements = r.Settlements.Take(TableRows)
                .Select(s => new SettlementDoc(s.Date, s.Courier, Loc.EnumText(s.Method), s.Orders, s.Expected, s.Received)).ToList(),
            DrawerCloses = r.ShiftCloses.Take(TableRows)
                .Select(c => new DrawerCloseDoc(c.ClosedAt, c.Cashier, null, c.Expected, c.Counted, c.ExpectedLbp, c.CountedLbp)).ToList(),
        };
    }

    public Task<ReportDoc> PeriodAsync(string period, DateTime today, CancellationToken ct = default)
    {
        var (from, to) = PeriodRange(period, today);
        return ReportAsync(from, to, ct);
    }

    public async Task<LiveDoc> LiveAsync(CancellationToken ct = default)
    {
        var drawers = new List<OpenDrawerDoc>();
        foreach (var shift in await shifts.GetOpenShiftsAsync(ct))
        {
            var summary = await shifts.GetSummaryAsync(shift.Id, ct);
            var cashier = shift.CurrentUser?.FullName ?? shift.User?.FullName ?? summary.Cashier;
            drawers.Add(new OpenDrawerDoc(shift.TillName, cashier, shift.OpenedAt, summary.SalesCount, summary.ExpectedCash, summary.ExpectedCashLbp));
        }

        await using var db = await factory.CreateDbContextAsync(ct);
        var sales = await db.Sales.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt).Take(RecentSales)
            .Select(s => new
            {
                s.ReceiptNumber, s.CreatedAt, Cashier = s.User != null ? s.User.FullName : null, s.Total, s.Status, s.Channel,
                Items = s.Lines.Sum(l => (int?)l.Quantity) ?? 0,
            })
            .ToListAsync(ct);

        var closes = await db.Shifts.AsNoTracking()
            .Where(s => s.Status == ShiftStatus.Closed && s.ClosedAt != null)
            .OrderByDescending(s => s.ClosedAt).Take(RecentCloses)
            .Select(s => new DrawerCloseDoc(
                s.ClosedAt!.Value, s.User != null ? s.User.FullName : "", s.TillName, s.ExpectedCash, s.CountedCash, s.ExpectedCashLbp, s.CountedCashLbp))
            .ToListAsync(ct);

        var active = db.ProductVariants.AsNoTracking().Where(v => v.IsActive && v.Product!.IsActive);
        var low = await active.CountAsync(v => v.StockQuantity <= v.ReorderLevel, ct);
        var outOfStock = await active.CountAsync(v => v.StockQuantity <= 0, ct);

        return new LiveDoc(
            drawers,
            sales.Select(s => new RecentSaleDoc(
                s.ReceiptNumber, s.CreatedAt, s.Cashier, s.Total, s.Items, s.Status == SaleStatus.Voided ? "voided" : "completed",
                s.Channel == SalesChannel.InStore ? null : Loc.EnumText(s.Channel))).ToList(),
            closes, low, outOfStock);
    }

    public async Task<StockDoc> StockAsync(DateTime today, CancellationToken ct = default)
    {
        var valuation = await reports.GetInventoryValuationAsync(ct);
        var alerts = await reports.GetStockAlertsAsync(today.Date.AddDays(-SlowSinceDays), today.Date.AddDays(1), ct);
        return new StockDoc(
            valuation.Skus, valuation.Units, valuation.CostValue, valuation.RetailValue,
            alerts.LowStock.Count, valuation.OutOfStockCount, SlowSinceDays,
            Rows(valuation.ByCategoryCost.Take(TableRows)),
            alerts.LowStock.Take(StockRows).Select(a => new LowStockDoc(a.Product, a.Variant, a.Sku, a.Stock, a.ReorderLevel)).ToList(),
            alerts.SlowMovers.Take(StockRows).Select(a => new SlowMoverDoc(a.Product, a.Variant, a.Stock, a.CostValue, a.LastSold)).ToList());
    }

    /// <summary>
    /// The accounts allowed to sign in: active managers and admins whose password is their own (not one still to be
    /// changed at first sign-in).
    /// </summary>
    public async Task<List<OnlineUser>> UsersAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var users = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && !u.MustChangePassword && (u.Role == UserRole.Manager || u.Role == UserRole.Admin))
            .OrderBy(u => u.Username)
            .Select(u => new { u.Username, u.FullName, u.Role, u.PasswordHash })
            .ToListAsync(ct);
        var result = new List<OnlineUser>();
        foreach (var u in users)
        {
            if (Verifier(u.PasswordHash) is not { } v) continue;
            result.Add(new OnlineUser(u.Username.Trim().ToLowerInvariant(), u.FullName, u.Role == UserRole.Admin ? "admin" : "manager", v.Salt, v.Iterations, v.Verifier));
        }
        return result;
    }

    /// <summary>
    /// From a stored password (<c>PBKDF2$iterations$salt$key</c>): the salt and rounds the browser needs, and a SHA-256 of
    /// the key. Someone who reads the website's database still has to guess the password through 100,000 PBKDF2 rounds.
    /// </summary>
    public static (string Salt, int Iterations, string Verifier)? Verifier(string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "PBKDF2" || !int.TryParse(parts[1], out var iterations)) return null;
        try
        {
            var key = Convert.FromBase64String(parts[3]);
            if (key.Length != 32 || Convert.FromBase64String(parts[2]).Length is < 8 or > 64) return null;
            return (parts[2], iterations, Convert.ToBase64String(SHA256.HashData(key)));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static List<ReportRow> Rows(IEnumerable<NamedAmount> rows) =>
        rows.Select(r => new ReportRow(r.Name, r.Quantity, r.Amount, r.Count, r.Profit, r.SharePercent)).ToList();

    private static string Day(DateTime date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
