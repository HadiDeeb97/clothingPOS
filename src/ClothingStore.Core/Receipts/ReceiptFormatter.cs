using System.Text;

namespace ClothingStore.Core.Receipts;

/// <summary>
/// Lays a <see cref="ReceiptDocument"/> out as fixed-width text, suitable for 80mm thermal
/// printers (42 columns) or a monospaced print preview.
/// </summary>
public static class ReceiptFormatter
{
    public static IReadOnlyList<string> Format(ReceiptDocument doc, int width = 42)
    {
        width = Math.Clamp(width, 24, 80);
        var lines = new List<string>();
        string M(decimal v) => Money.Format(v, doc.CurrencySymbol);
        var rule = new string('-', width);

        lines.Add(Center(doc.StoreName.ToUpperInvariant(), width));
        foreach (var part in new[] { doc.StoreAddress, doc.StorePhone })
            if (!string.IsNullOrWhiteSpace(part))
                foreach (var l in part.Split('\n')) lines.Add(Center(l.Trim(), width));
        if (!string.IsNullOrWhiteSpace(doc.TaxNumber))
            lines.Add(Center($"Tax No: {doc.TaxNumber}", width));

        lines.Add(rule);
        lines.Add(Center(doc.Title.ToUpperInvariant(), width));
        if (doc.IsCopy) lines.Add(Center("*** COPY ***", width));
        lines.Add(rule);
        lines.Add(Pair("No:", doc.Number, width));
        lines.Add(Pair("Date:", doc.Date.ToString("yyyy-MM-dd HH:mm"), width));
        lines.Add(Pair("Cashier:", doc.Cashier, width));
        if (!string.IsNullOrWhiteSpace(doc.Customer)) lines.Add(Pair("Customer:", doc.Customer, width));
        if (!string.IsNullOrWhiteSpace(doc.Reference)) lines.Add(Pair("Ref:", doc.Reference, width));
        lines.Add(rule);

        foreach (var item in doc.Lines)
        {
            foreach (var l in Wrap(item.Description, width)) lines.Add(l);
            if (!string.IsNullOrWhiteSpace(item.Detail)) lines.Add(Truncate("  " + item.Detail, width));
            lines.Add(Pair($"  {item.Quantity} x {M(item.UnitPrice)}", M(item.Total), width));
            if (item.Discount != 0) lines.Add(Pair("  Discount", M(-item.Discount), width));
        }

        lines.Add(rule);
        lines.Add(Pair("Subtotal", M(doc.Subtotal), width));
        if (doc.Discount != 0) lines.Add(Pair("Discount", M(-doc.Discount), width));
        var taxLabel = doc.PricesIncludeTax ? $"Incl. tax {doc.TaxRate:0.##}%" : $"Tax {doc.TaxRate:0.##}%";
        lines.Add(Pair(taxLabel, M(doc.Tax), width));
        lines.Add(new string('=', width));
        lines.Add(Pair(doc.TotalLabel, M(doc.Total), width));
        lines.Add(new string('=', width));

        foreach (var p in doc.Payments) lines.Add(Pair(p.Label, M(p.Amount), width));
        if (doc.Change > 0) lines.Add(Pair("Change", M(doc.Change), width));

        if (doc.ExtraLines.Count > 0)
        {
            lines.Add(rule);
            foreach (var extra in doc.ExtraLines)
                foreach (var l in Wrap(extra, width)) lines.Add(l);
        }

        var itemCount = doc.Lines.Sum(l => l.Quantity);
        lines.Add(rule);
        lines.Add(Center($"Items: {itemCount}", width));

        if (!string.IsNullOrWhiteSpace(doc.Footer))
        {
            lines.Add("");
            foreach (var footerLine in doc.Footer.Split('\n'))
                foreach (var l in Wrap(footerLine.Trim(), width)) lines.Add(Center(l, width));
        }

        return lines;
    }

    public static string FormatText(ReceiptDocument doc, int width = 42) =>
        string.Join(Environment.NewLine, Format(doc, width));

    internal static string Pair(string left, string right, int width)
    {
        var space = width - right.Length - 1;
        if (space < 1) return Truncate(left + " " + right, width);
        return Truncate(left, space).PadRight(space) + " " + right;
    }

    internal static string Center(string text, int width)
    {
        text = Truncate(text, width);
        var pad = (width - text.Length) / 2;
        return new string(' ', pad) + text;
    }

    private static string Truncate(string text, int width) => text.Length <= width ? text : text[..width];

    internal static IEnumerable<string> Wrap(string text, int width)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield return "";
            yield break;
        }

        var line = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var w = word;
            while (w.Length > width)
            {
                if (line.Length > 0) { yield return line.ToString(); line.Clear(); }
                yield return w[..width];
                w = w[width..];
            }
            if (line.Length + w.Length + (line.Length > 0 ? 1 : 0) > width)
            {
                yield return line.ToString();
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(w);
        }
        if (line.Length > 0) yield return line.ToString();
    }
}
