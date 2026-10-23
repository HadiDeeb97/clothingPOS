namespace ClothingStore.Data.OnlineReports;

// What the POS uploads to the shop's reports website (cloud/reports-worker). Sent as camelCase JSON; the website only
// displays these, so every figure here is the POS's own (ReportService). No customer names or phone numbers.

/// <summary>The shop, for the website's header and number formatting.</summary>
public sealed record StoreDoc(string Name, string Currency, bool Lbp, decimal LbpRate, string Language);

/// <summary>A row of a report table: a payment method, category, product, cashier, size…</summary>
public sealed record ReportRow(string Name, int Qty, decimal Amount, int Count, decimal Profit, decimal Share);

public sealed record ReportTotals(
    decimal TotalSales, decimal GrossSales, decimal Discounts, decimal Tax, decimal NetRevenue, decimal CostOfGoods,
    decimal GrossProfit, decimal MarginPercent, int Transactions, int ItemsSold, decimal AverageBasket, decimal Refunds,
    int ReturnsCount, int VoidedCount, decimal DeliveryFees);

/// <summary>Percent change against the same length of time just before (null when there was nothing before).</summary>
public sealed record ReportChange(decimal? Sales, decimal? Transactions, decimal? Profit);

public sealed record DayPoint(string Date, int Count, decimal Total);

public sealed record HourPoint(int Hour, int Count, decimal Total);

public sealed record SettlementDoc(DateTime Date, string? Courier, string Method, int Orders, decimal Expected, decimal Received);

public sealed record DrawerCloseDoc(
    DateTime ClosedAt, string Cashier, string? Till, decimal Expected, decimal? Counted, decimal ExpectedLbp, decimal? CountedLbp);

/// <summary>A full report for a period, a day or a month (the POS Reports page, without customer names).</summary>
public sealed record ReportDoc
{
    /// <summary>First and last day covered (yyyy-MM-dd, both included).</summary>
    public required string From { get; init; }
    public required string To { get; init; }
    public required ReportTotals Totals { get; init; }
    public required ReportChange Change { get; init; }
    public IReadOnlyList<DayPoint> ByDay { get; init; } = [];
    public IReadOnlyList<HourPoint> ByHour { get; init; } = [];
    public IReadOnlyList<ReportRow> Payments { get; init; } = [];
    public IReadOnlyList<ReportRow> Categories { get; init; } = [];
    public IReadOnlyList<ReportRow> Products { get; init; } = [];
    public IReadOnlyList<ReportRow> Cashiers { get; init; } = [];
    public IReadOnlyList<ReportRow> Sizes { get; init; } = [];
    public IReadOnlyList<ReportRow> Channels { get; init; } = [];
    public IReadOnlyList<ReportRow> ReturnsByReason { get; init; } = [];
    public IReadOnlyList<ReportRow> TopReturned { get; init; } = [];
    public IReadOnlyList<ReportRow> RefundsByMethod { get; init; } = [];
    public IReadOnlyList<SettlementDoc> Settlements { get; init; } = [];
    public IReadOnlyList<DrawerCloseDoc> DrawerCloses { get; init; } = [];
}

public sealed record OpenDrawerDoc(string? Till, string Cashier, DateTime OpenedAt, int Sales, decimal ExpectedCash, decimal ExpectedCashLbp);

public sealed record RecentSaleDoc(string Receipt, DateTime Time, string? Cashier, decimal Total, int Items, string Status, string? Channel);

/// <summary>What is happening now: open drawers, the last sales, recent drawer closes, low stock.</summary>
public sealed record LiveDoc(
    IReadOnlyList<OpenDrawerDoc> OpenDrawers, IReadOnlyList<RecentSaleDoc> RecentSales, IReadOnlyList<DrawerCloseDoc> RecentCloses,
    int LowStockCount, int OutOfStockCount);

public sealed record LowStockDoc(string Product, string Variant, string Sku, int Stock, int Reorder);

public sealed record SlowMoverDoc(string Product, string Variant, int Stock, decimal Value, DateTime? LastSold);

public sealed record StockDoc(
    int Skus, int Units, decimal CostValue, decimal RetailValue, int LowStockCount, int OutOfStockCount, int SlowSinceDays,
    IReadOnlyList<ReportRow> ByCategory, IReadOnlyList<LowStockDoc> LowStock, IReadOnlyList<SlowMoverDoc> SlowMovers);

/// <summary>
/// An account allowed to sign in (managers and admins). Never the password: the PBKDF2 salt the browser needs, and a
/// SHA-256 of the stored PBKDF2 key that the website compares with what the browser works out.
/// </summary>
public sealed record OnlineUser(string Username, string Name, string Role, string Salt, int Iterations, string Verifier);
