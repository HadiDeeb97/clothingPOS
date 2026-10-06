using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Localization;
using ClothingStore.Core.Receipts;
using ClothingStore.Data.Services;

namespace ClothingStore.Desktop.Services;

/// <summary>
/// Turns sales, returns and shift summaries into printable documents. Customer receipts use the receipt language
/// from Settings; internal reports (X/Z, purchase orders) use the language of the screen.
/// </summary>
public static class ReceiptBuilder
{
    public static ReceiptDocument FromSale(Sale sale, StoreSettings s, bool isCopy = false)
    {
        var lang = s.ReceiptLanguage;
        string R(string key, params object?[] args) => Loc.Format(lang, key, args);

        var extra = new List<string>();
        if (sale.Status == SaleStatus.Voided)
            extra.Add(R("Receipt.Voided", sale.VoidedAt, sale.VoidReason));
        if (sale.Customer is not null)
        {
            if (sale.LoyaltyPointsEarned > 0) extra.Add(R("Receipt.PointsEarned", sale.LoyaltyPointsEarned));
            if (sale.LoyaltyPointsRedeemed > 0) extra.Add(R("Receipt.PointsRedeemed", sale.LoyaltyPointsRedeemed));
            extra.Add(R("Receipt.PointsBalance", sale.Customer.LoyaltyPoints));
            if (sale.Customer.StoreCredit > 0) extra.Add(R("Receipt.CreditBalance", Money.Format(sale.Customer.StoreCredit, s.CurrencySymbol)));
        }
        if (!string.IsNullOrWhiteSpace(sale.Notes)) extra.Add(R("Receipt.Note", sale.Notes));

        var payments = sale.Payments
            .Select(p => new ReceiptPayment(PaymentLabel(lang, p.Method, p.Reference), p.Method == PaymentMethod.Cash ? sale.CashTendered : p.Amount))
            .ToList();

        return new ReceiptDocument
        {
            Title = R("Receipt.SalesTitle"),
            Language = lang,
            StoreName = s.StoreName,
            StoreAddress = s.Address,
            StorePhone = s.Phone,
            TaxNumber = s.TaxNumber,
            Number = sale.ReceiptNumber,
            Date = sale.CreatedAt,
            Cashier = sale.User?.FullName ?? "",
            Customer = sale.Customer?.FullName,
            Lines = sale.Lines.Select(l => new ReceiptLine(
                l.ProductName, Detail(l.VariantDescription, l.Sku), l.Quantity, l.UnitPrice, l.DiscountAmount, l.UnitPrice * l.Quantity)).ToList(),
            Subtotal = sale.Subtotal,
            Discount = sale.DiscountTotal,
            Tax = sale.TaxTotal,
            TaxRate = s.TaxRate,
            PricesIncludeTax = s.PricesIncludeTax,
            Total = sale.Total,
            Payments = payments,
            Change = sale.ChangeGiven,
            ExtraLines = extra,
            Footer = s.ReceiptFooter,
            CurrencySymbol = s.CurrencySymbol,
            IsCopy = isCopy,
        };
    }

    public static ReceiptDocument FromReturn(SaleReturn ret, Sale sale, StoreSettings s, string cashier)
    {
        var lang = s.ReceiptLanguage;
        string R(string key, params object?[] args) => Loc.Format(lang, key, args);

        var lines = ret.Lines.Select(rl =>
        {
            var sl = sale.Lines.First(l => l.Id == rl.SaleLineId);
            return new ReceiptLine(sl.ProductName, Detail(sl.VariantDescription, sl.Sku) + (rl.Restocked ? "" : R("Receipt.NotRestocked")),
                rl.Quantity, Money.Round(rl.RefundAmount / rl.Quantity), 0, rl.RefundAmount);
        }).ToList();

        var extra = new List<string>();
        if (!string.IsNullOrWhiteSpace(ret.Reason)) extra.Add(R("Receipt.Reason", ret.Reason));
        if (ret.LoyaltyPointsRestored > 0) extra.Add(R("Receipt.PointsBack", ret.LoyaltyPointsRestored));
        if (ret.LoyaltyPointsRemoved > 0) extra.Add(R("Receipt.PointsTaken", ret.LoyaltyPointsRemoved));

        return new ReceiptDocument
        {
            Title = R("Receipt.ReturnTitle"),
            Language = lang,
            StoreName = s.StoreName,
            StoreAddress = s.Address,
            StorePhone = s.Phone,
            TaxNumber = s.TaxNumber,
            Number = ret.ReturnNumber,
            Reference = R("Receipt.OriginalReceipt", sale.ReceiptNumber),
            Date = ret.CreatedAt,
            Cashier = cashier,
            Customer = sale.Customer?.FullName,
            Lines = lines,
            Subtotal = ret.TotalRefund,
            Tax = ret.TaxRefund,
            TaxRate = s.TaxRate,
            PricesIncludeTax = true, // refunds always include the tax that was charged
            Total = ret.TotalRefund,
            TotalLabel = R("Receipt.Refund"),
            Payments = ret.Refunds
                .GroupBy(r => r.Method)
                .OrderBy(g => g.Key)
                .Select(g => new ReceiptPayment(R("Receipt.RefundedTo", Loc.Get(lang, $"Enum.RefundMethod.{g.Key}")), g.Sum(r => r.Amount)))
                .ToList(),
            ExtraLines = extra,
            CurrencySymbol = s.CurrencySymbol,
        };
    }

    public static IReadOnlyList<string> ShiftReport(ShiftSummary sum, StoreSettings s)
    {
        const int w = 42;
        string M(decimal v) => Money.Format(v, s.CurrencySymbol);
        string Row(string l, string r) => l.PadRight(w - r.Length - 1)[..(w - r.Length - 1)] + " " + r;
        var rule = new string('-', w);

        var lines = new List<string>
        {
            s.StoreName.ToUpperInvariant(),
            Loc.T(sum.ClosedAt is null ? "Report.XTitle" : "Report.ZTitle"),
            rule,
            Row(Loc.T("Report.Shift"), sum.ShiftId.ToString()),
            Row(Loc.T("Common.Cashier"), sum.Cashier),
            Row(Loc.T("Report.Opened"), sum.OpenedAt.ToString("yyyy-MM-dd HH:mm")),
            Row(Loc.T("Report.Closed"), sum.ClosedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-"),
            rule,
            Row(Loc.T("Reports.Transactions"), sum.SalesCount.ToString()),
            Row(Loc.T("Report.Voided"), sum.VoidedCount.ToString()),
            Row(Loc.T("Reports.ItemsSold"), sum.ItemsSold.ToString()),
            Row(Loc.T("Reports.GrossSales"), M(sum.GrossSales)),
            Row(Loc.T("Common.Discounts"), M(-sum.Discounts)),
            Row(Loc.T("Common.Tax"), M(sum.Tax)),
            Row(Loc.T("Reports.TotalSales"), M(sum.TotalSales)),
            rule,
            Loc.T("Common.Payments"),
        };
        foreach (var method in Enum.GetValues<PaymentMethod>())
            if (sum.Payments.TryGetValue(method, out var amount))
                lines.Add(Row("  " + Loc.EnumText(method), M(amount)));

        lines.Add(rule);
        lines.Add(Loc.T("Report.Returns", sum.ReturnsCount));
        foreach (var method in Enum.GetValues<RefundMethod>())
            if (sum.Refunds.TryGetValue(method, out var amount))
                lines.Add(Row("  " + Loc.EnumText(method), M(-amount)));

        lines.Add(rule);
        lines.Add(Loc.T("Report.CashDrawer"));
        lines.Add(Row("  " + Loc.T("Shift.OpeningFloat"), M(sum.OpeningFloat)));
        lines.Add(Row("  " + Loc.T("Shift.CashSales"), M(sum.CashSales)));
        lines.Add(Row("  " + Loc.T("Shift.PayIns"), M(sum.PayIns)));
        lines.Add(Row("  " + Loc.T("Shift.PayOuts"), M(-sum.PayOuts)));
        lines.Add(Row("  " + Loc.T("Shift.CashRefunds"), M(-sum.CashRefunds)));
        lines.Add(Row("  " + Loc.T("Shift.Expected"), M(sum.ExpectedCash)));
        if (sum.CountedCash is { } counted)
        {
            lines.Add(Row("  " + Loc.T("Report.Counted"), M(counted)));
            lines.Add(Row("  " + Loc.T("Report.OverShort"), M(sum.Variance ?? 0)));
        }

        if (sum.CashMovements.Count > 0)
        {
            lines.Add(rule);
            lines.Add(Loc.T("Report.CashMovements"));
            foreach (var m in sum.CashMovements)
                lines.Add(Row($"  {m.CreatedAt:HH:mm} {m.Reason}", M(m.Type == CashMovementType.PayIn ? m.Amount : -m.Amount)));
        }

        lines.Add(rule);
        lines.Add(Loc.T("Report.Printed", DateTime.Now));
        return lines;
    }

    public static IReadOnlyList<string> PurchaseOrder(PurchaseOrder po, StoreSettings s)
    {
        string M(decimal v) => Money.Format(v, s.CurrencySymbol);
        var lines = new List<string>
        {
            s.StoreName.ToUpperInvariant(),
            s.Address ?? "",
            "",
            Loc.T("Report.PurchaseOrder", po.OrderNumber),
            Loc.T("Report.Supplier", po.Supplier?.Name),
            Loc.T("Report.Date", po.CreatedAt),
            po.ExpectedDate is { } d ? Loc.T("Report.Expected", d) : "",
            Loc.T("Report.Status", Loc.EnumText(po.Status)),
            new string('-', 78),
            Loc.T("Report.PoHeader"),
            new string('-', 78),
        };
        foreach (var l in po.Lines)
        {
            var name = l.ProductVariant?.DisplayName ?? "";
            if (name.Length > 30) name = name[..30];
            var sku = l.ProductVariant?.Sku ?? "";
            if (sku.Length > 20) sku = sku[..20];
            lines.Add($"{sku,-20} {name,-30} {l.QuantityOrdered,5} {M(l.UnitCost),9} {M(l.LineTotal),10}");
        }
        lines.Add(new string('-', 78));
        lines.Add($"{Loc.T("Report.Total"),-57} {po.TotalUnits,5} {"",9} {M(po.Total),10}".TrimEnd());
        if (!string.IsNullOrWhiteSpace(po.Notes)) { lines.Add(""); lines.Add(Loc.T("Report.Notes", po.Notes)); }
        return lines;
    }

    private static string PaymentLabel(string language, PaymentMethod method, string? reference) =>
        Loc.Get(language, $"Enum.PaymentMethod.{method}") + (string.IsNullOrWhiteSpace(reference) ? "" : $" ({reference})");

    private static string Detail(string variant, string sku) =>
        string.IsNullOrWhiteSpace(variant) ? sku : $"{variant}  {sku}";
}
