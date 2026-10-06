namespace ClothingStore.Core.Receipts;

public sealed record ReceiptLine(
    string Description,
    string? Detail,
    int Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal Total);

public sealed record ReceiptPayment(string Label, decimal Amount);

/// <summary>Printer-agnostic receipt content for sales, returns and reprints.</summary>
public sealed record ReceiptDocument
{
    public required string Title { get; init; }
    public required string StoreName { get; init; }
    public string? StoreAddress { get; init; }
    public string? StorePhone { get; init; }
    public string? TaxNumber { get; init; }
    public required string Number { get; init; }
    public required DateTime Date { get; init; }
    public required string Cashier { get; init; }
    public string? Customer { get; init; }
    public string? Reference { get; init; }
    public IReadOnlyList<ReceiptLine> Lines { get; init; } = [];
    public decimal Subtotal { get; init; }
    public decimal Discount { get; init; }
    public decimal Tax { get; init; }
    public decimal TaxRate { get; init; }
    public bool PricesIncludeTax { get; init; }
    public decimal Total { get; init; }
    /// <summary>Label of the total line; null = "TOTAL" in the receipt language.</summary>
    public string? TotalLabel { get; init; }
    public IReadOnlyList<ReceiptPayment> Payments { get; init; } = [];
    public decimal Change { get; init; }
    public IReadOnlyList<string> ExtraLines { get; init; } = [];
    public string? Footer { get; init; }
    public string CurrencySymbol { get; init; } = "$";
    public bool IsCopy { get; init; }

    /// <summary>Language of the printed labels ("en" or "ar").</summary>
    public string Language { get; init; } = "en";

    /// <summary>Optional second currency line under the total (e.g. the LBP amount at today's rate).</summary>
    public string? SecondaryTotal { get; init; }
}
