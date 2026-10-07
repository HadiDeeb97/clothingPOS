using System.IO;
using System.Text.Json;

namespace ClothingStore.Desktop.Infrastructure;

/// <summary>
/// Per-PC preferences (language on the sign-in screen, sidebar, panel sizes and column widths, printers), kept in
/// %LocalAppData%\ClothingStorePOS\preferences.json. Losing the file only resets the layout.
/// </summary>
public sealed class LocalPreferences
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClothingStorePOS", "preferences.json");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static LocalPreferences Current { get; } = Load();

    public string? Language { get; set; }
    public bool SidebarCollapsed { get; set; }
    public Dictionary<string, string> Layout { get; set; } = [];

    /// <summary>Label size and content last used on this PC (each PC has its own label printer).</summary>
    public Services.LabelOptions? Labels { get; set; }

    /// <summary>Printer receipts go to without asking (null = show the Windows print dialog).</summary>
    public string? ReceiptPrinter { get; set; }

    /// <summary>Printer price labels go to without asking (null = show the Windows print dialog).</summary>
    public string? LabelPrinter { get; set; }

    /// <summary>Copies of a receipt printed by default on this PC.</summary>
    public int ReceiptCopies { get; set; } = 1;

    /// <summary>Delivery fee of the last online order taken here, offered for the next one.</summary>
    public decimal? LastDeliveryFee { get; set; }

    /// <summary>Delivery company / driver of the last online order taken here, offered for the next one.</summary>
    public string? LastCourier { get; set; }

    /// <summary>Day the license expiry warning was last shown here (shown once a day).</summary>
    public DateTime? LicenseWarnedOn { get; set; }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Preferences are a convenience; never fail the app over them.
        }
    }

    private static LocalPreferences Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<LocalPreferences>(File.ReadAllText(FilePath), Json) ?? new LocalPreferences();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        return new LocalPreferences();
    }
}
