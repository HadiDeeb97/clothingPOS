using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Receipts;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Converters;

namespace ClothingStore.Desktop.Services;

/// <summary>Turns sales, returns and shift summaries into printable documents.</summary>
public static class ReceiptBuilder
{
    public static ReceiptDocument FromSale(Sale sale, StoreSettings s, bool isCopy = false)
    {
        var extra = new List<string>();
        if (sale.Status == SaleStatus.Voided)
            extra.Add($"*** VOIDED {sale.VoidedAt:yyyy-MM-dd HH:mm}: {sale.VoidReason} ***");
        if (sale.Customer is not null)
        {
            if (sale.LoyaltyPointsEarned > 0) extra.Add($"Points earned: {sale.LoyaltyPointsEarned}");
            if (sale.LoyaltyPointsRedeemed > 0) extra.Add($"Points redeemed: {sale.LoyaltyPointsRedeemed}");
            extra.Add($"Points balance: {sale.Customer.LoyaltyPoints}");
            if (sale.Customer.StoreCredit > 0) extra.Add($"Store credit balance: {Money.Format(sale.Customer.StoreCredit, s.CurrencySymbol)}");
        }
        if (!string.IsNullOrWhiteSpace(sale.Notes)) extra.Add($"Note: {sale.Notes}");

        var payments = sale.Payments
            .Select(p => new ReceiptPayment(PaymentLabel(p.Method, p.Reference), p.Method == PaymentMethod.Cash ? sale.CashTendered : p.Amount))
            .ToList();

        return new ReceiptDocument
        {
            Title = "Sales Receipt",
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
        var lines = ret.Lines.Select(rl =>
        {
            var sl = sale.Lines.First(l => l.Id == rl.SaleLineId);
            return new ReceiptLine(sl.ProductName, Detail(sl.VariantDescription, sl.Sku) + (rl.Restocked ? "" : " (not restocked)"),
                rl.Quantity, Money.Round(rl.RefundAmount / rl.Quantity), 0, rl.RefundAmount);
        }).ToList();

        var extra = new List<string>();
        if (!string.IsNullOrWhiteSpace(ret.Reason)) extra.Add($"Reason: {ret.Reason}");
        extra.Add("Customer signature: ____________________");

        return new ReceiptDocument
        {
            Title = "Return / Refund",
            StoreName = s.StoreName,
            StoreAddress = s.Address,
            StorePhone = s.Phone,
            TaxNumber = s.TaxNumber,
            Number = ret.ReturnNumber,
            Reference = $"Original receipt {sale.ReceiptNumber}",
            Date = ret.CreatedAt,
            Cashier = cashier,
            Customer = sale.Customer?.FullName,
            Lines = lines,
            Subtotal = ret.TotalRefund,
            Tax = ret.TaxRefund,
            TaxRate = s.TaxRate,
            PricesIncludeTax = true, // refunds always include the tax that was charged
            Total = ret.TotalRefund,
            TotalLabel = "REFUND",
            Payments = [new ReceiptPayment($"Refunded to {EnumDisplayConverter.Humanize(ret.RefundMethod.ToString())}", ret.TotalRefund)],
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
            sum.ClosedAt is null ? "X REPORT (shift in progress)" : "Z REPORT (end of shift)",
            rule,
            Row("Shift #", sum.ShiftId.ToString()),
            Row("Cashier", sum.Cashier),
            Row("Opened", sum.OpenedAt.ToString("yyyy-MM-dd HH:mm")),
            Row("Closed", sum.ClosedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-"),
            rule,
            Row("Transactions", sum.SalesCount.ToString()),
            Row("Voided", sum.VoidedCount.ToString()),
            Row("Items sold", sum.ItemsSold.ToString()),
            Row("Gross sales", M(sum.GrossSales)),
            Row("Discounts", M(-sum.Discounts)),
            Row("Tax", M(sum.Tax)),
            Row("Total sales", M(sum.TotalSales)),
            rule,
            "PAYMENTS",
        };
        foreach (var method in Enum.GetValues<PaymentMethod>())
            if (sum.Payments.TryGetValue(method, out var amount))
                lines.Add(Row("  " + EnumDisplayConverter.Humanize(method.ToString()), M(amount)));

        lines.Add(rule);
        lines.Add($"RETURNS ({sum.ReturnsCount})");
        foreach (var method in Enum.GetValues<RefundMethod>())
            if (sum.Refunds.TryGetValue(method, out var amount))
                lines.Add(Row("  " + EnumDisplayConverter.Humanize(method.ToString()), M(-amount)));

        lines.Add(rule);
        lines.Add("CASH DRAWER");
        lines.Add(Row("  Opening float", M(sum.OpeningFloat)));
        lines.Add(Row("  Cash sales", M(sum.CashSales)));
        lines.Add(Row("  Pay-ins", M(sum.PayIns)));
        lines.Add(Row("  Pay-outs", M(-sum.PayOuts)));
        lines.Add(Row("  Cash refunds", M(-sum.CashRefunds)));
        lines.Add(Row("  Expected in drawer", M(sum.ExpectedCash)));
        if (sum.CountedCash is { } counted)
        {
            lines.Add(Row("  Counted", M(counted)));
            lines.Add(Row("  Over / (short)", M(sum.Variance ?? 0)));
        }

        if (sum.CashMovements.Count > 0)
        {
            lines.Add(rule);
            lines.Add("CASH MOVEMENTS");
            foreach (var m in sum.CashMovements)
                lines.Add(Row($"  {m.CreatedAt:HH:mm} {m.Reason}", M(m.Type == CashMovementType.PayIn ? m.Amount : -m.Amount)));
        }

        lines.Add(rule);
        lines.Add($"Printed {DateTime.Now:yyyy-MM-dd HH:mm}");
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
            $"PURCHASE ORDER {po.OrderNumber}",
            $"Supplier: {po.Supplier?.Name}",
            $"Date:     {po.CreatedAt:yyyy-MM-dd}",
            po.ExpectedDate is { } d ? $"Expected: {d:yyyy-MM-dd}" : "",
            $"Status:   {EnumDisplayConverter.Humanize(po.Status.ToString())}",
            new string('-', 78),
            $"{"SKU",-20} {"Item",-30} {"Qty",5} {"Cost",9} {"Total",10}",
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
        lines.Add($"{"TOTAL",-57} {po.TotalUnits,5} {"",9} {M(po.Total),10}".TrimEnd());
        if (!string.IsNullOrWhiteSpace(po.Notes)) { lines.Add(""); lines.Add("Notes: " + po.Notes); }
        return lines;
    }

    private static string PaymentLabel(PaymentMethod method, string? reference) =>
        EnumDisplayConverter.Humanize(method.ToString()) + (string.IsNullOrWhiteSpace(reference) ? "" : $" ({reference})");

    private static string Detail(string variant, string sku) =>
        string.IsNullOrWhiteSpace(variant) ? sku : $"{variant}  {sku}";
}
