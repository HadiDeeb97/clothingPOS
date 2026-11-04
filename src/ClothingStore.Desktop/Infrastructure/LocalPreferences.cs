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

    /// <summary>A cash drawer is plugged into this PC's receipt printer (opened by a signal sent to the printer).</summary>
    public bool CashDrawer { get; set; }

    /// <summary>Which signal opens it: almost every printer takes the standard one.</summary>
    public CashDrawerSignal DrawerSignal { get; set; } = CashDrawerSignal.Standard;

    public bool DrawerOnCashSale { get; set; } = true;
    public bool DrawerOnCashRefund { get; set; } = true;
    public bool DrawerOnCashMovement { get; set; } = true;
    public bool DrawerOnShift { get; set; } = true;

    /// <summary>A second screen facing the customer shows the cart, totals and change (Settings → Customer display).</summary>
    public bool CustomerDisplay { get; set; }

    /// <summary>Windows name of that screen (e.g. \\.\DISPLAY2); null or unplugged = the first screen that isn't the main one.</summary>
    public string? CustomerDisplayScreen { get; set; }

    /// <summary>New product: "Save &amp; add new" keeps the details for the next product (only the name is cleared).</summary>
    public bool KeepProductDetails { get; set; }

    /// <summary>Copies of a receipt printed by default on this PC.</summary>
    public int ReceiptCopies { get; set; } = 1;

    /// <summary>Delivery fee of the last online order taken here, offered for the next one.</summary>
    public decimal? LastDeliveryFee { get; set; }

    /// <summary>Delivery company / driver of the last online order taken here, offered for the next one.</summary>
    public string? LastCourier { get; set; }

    /// <summary>The installed program put its shortcut on the desktop already (it isn't made again once deleted).</summary>
    public bool DesktopShortcutMade { get; set; }

    /// <summary>Text size (zoom) of the POS on this PC: 1 = 100%. Snapped to ZoomSteps when read.</summary>
    public double UiScale { get; set; } = 1.0;

    /// <summary>Font of this PC's screens (null = the app's own); see <see cref="UiFont"/>.</summary>
    public string? UiFont { get; set; }

    /// <summary>This PC runs the POS full screen (no title bar or taskbar); it starts that way next time too.</summary>
    public bool FullScreen { get; set; }

    /// <summary>Day the license expiry warning was last shown here (shown once a day).</summary>
    public DateTime? LicenseWarnedOn { get; set; }

    /// <summary>
    /// Which column sizing the saved table widths come from. Widths saved before 1.3.3 may be squeezed (dragging one column
    /// used to shrink the others), so they are forgotten once and every table starts fitted to the screen again.
    /// </summary>
    public int ColumnsVersion { get; set; }

    private const int CurrentColumnsVersion = 2;

    // ---- This till's copy in the shop database (see TillSettingsSync) ----

    /// <summary>Changes made here that haven't reached the shop database yet; they are sent at the next chance.</summary>
    public bool TillSyncPending { get; set; }

    /// <summary>Set once this till's settings are kept in the shop database: from then on every save is sent there too.</summary>
    public static bool KeepsTillCopy { get; set; }

    /// <summary>Raised after a save that changed something (the till's copy in the shop database follows).</summary>
    public static event EventHandler? Saved;

    private static readonly object Writing = new();

    public void Save() => Save(notify: true);

    /// <param name="notify">False for the till sync's own bookkeeping, which mustn't be sent back to it.</param>
    internal void Save(bool notify)
    {
        if (notify && KeepsTillCopy) TillSyncPending = true; // on disk before it is sent, in case the app stops first
        lock (Writing)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                // Written whole to a new file, then swapped in: a save cut off halfway (Windows shutting down) leaves the
                // last complete file, not a half-written one the next start can't read.
                var temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(this, Json));
                File.Move(temp, FilePath, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Preferences are a convenience; never fail the app over them.
            }
        }
        if (notify) Saved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Settings kept per Windows account rather than per till (the desktop shortcut was made for this account).</summary>
    private static readonly HashSet<string> NotForTheTill = [nameof(DesktopShortcutMade), nameof(TillSyncPending)];

    /// <summary>This till's settings as kept in the shop database.</summary>
    public string ToTillJson()
    {
        var node = JsonSerializer.SerializeToNode(this, Json)!.AsObject();
        foreach (var name in NotForTheTill) node.Remove(name);
        return node.ToJsonString();
    }

    /// <summary>Takes this till's settings from the shop database (this PC's own copy may have been lost or reset).</summary>
    public bool ApplyTillJson(string json)
    {
        LocalPreferences? kept;
        try { kept = JsonSerializer.Deserialize<LocalPreferences>(json, Json); }
        catch (JsonException) { return false; }
        if (kept is null) return false;
        foreach (var property in typeof(LocalPreferences).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            if (property is { CanRead: true, CanWrite: true } && !NotForTheTill.Contains(property.Name))
                property.SetValue(this, property.GetValue(kept));
        return true;
    }

    private static LocalPreferences Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return Upgrade(JsonSerializer.Deserialize<LocalPreferences>(File.ReadAllText(FilePath), Json) ?? new LocalPreferences());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        return new LocalPreferences { ColumnsVersion = CurrentColumnsVersion };
    }

    private static LocalPreferences Upgrade(LocalPreferences prefs)
    {
        if (prefs.ColumnsVersion >= CurrentColumnsVersion) return prefs;
        foreach (var key in prefs.Layout.Keys.Where(k => k.StartsWith("cols:", StringComparison.Ordinal)).ToList()) prefs.Layout.Remove(key);
        prefs.ColumnsVersion = CurrentColumnsVersion;
        prefs.Save();
        return prefs;
    }
}
