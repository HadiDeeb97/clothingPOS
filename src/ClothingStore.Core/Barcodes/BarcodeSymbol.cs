namespace ClothingStore.Core.Barcodes;

/// <summary>The kind of barcode printed on labels (Settings → Receipts and labels).</summary>
public enum BarcodeType
{
    /// <summary>EAN-13 for 13-digit codes (what the app generates); anything else still prints as Code 128.</summary>
    Ean13 = 0,

    /// <summary>Code 128 for every code.</summary>
    Code128 = 1,
}

/// <summary>
/// A barcode ready to draw: its bar and space widths in modules (alternating, starting with a bar) and the empty
/// margin it needs on each side. <see cref="ModuleDots"/> picks a bar width that is a whole number of printer dots, so
/// every bar prints exactly as wide as it should; stretched to fit, bars come out uneven and scanners struggle.
/// </summary>
public sealed record BarcodeSymbol(IReadOnlyList<int> Widths, int QuietModules, BarcodeType Type)
{
    /// <summary>Width of the whole code in modules, margins included.</summary>
    public int TotalModules => Widths.Sum() + QuietModules * 2;

    /// <summary>The code to print for <paramref name="code"/>; null when it can't be drawn (empty, or characters no barcode holds).</summary>
    public static BarcodeSymbol? For(string? code, BarcodeType type)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        code = code.Trim();
        if (type == BarcodeType.Ean13 && Ean13.IsValid(code)) return new(Ean13.EncodeWidths(code), 11, BarcodeType.Ean13);
        try
        {
            return new(Code128.EncodeWidths(code), 10, BarcodeType.Code128);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Printer dots per module so the whole code, margins included, fits <paramref name="widthInches"/>: a whole number,
    /// at least 1. Above <paramref name="maxDots"/> the bars would only get needlessly wide.
    /// </summary>
    public int ModuleDots(double widthInches, double dpi, int maxDots = 6) =>
        Math.Clamp((int)Math.Floor(widthInches * dpi / TotalModules + 1e-9), 1, maxDots);

    /// <summary>Narrowest bar in millimetres at <paramref name="dots"/> dots per module (EAN-13 scans best from about 0.26 mm).</summary>
    public static double ModuleMm(int dots, double dpi) => dots * 25.4 / dpi;
}
