using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClothingStore.Desktop.Infrastructure;

/// <summary>The store logo, shown in the sidebar and sign-in screen and used as every window's icon.</summary>
public sealed partial class Branding : ObservableObject
{
    public static Branding Instance { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogo))]
    public partial ImageSource? Icon { get; private set; }

    public bool HasLogo => Icon is not null;

    /// <summary>Applies a PNG/JPEG logo (null removes it) to the open windows too.</summary>
    public void SetLogo(byte[]? image)
    {
        Icon = image is { Length: > 0 } ? Decode(image) : null;
        foreach (System.Windows.Window window in System.Windows.Application.Current.Windows)
            window.Icon = Icon;
    }

    public static BitmapImage Decode(byte[] image)
    {
        var bitmap = new BitmapImage();
        using var stream = new MemoryStream(image);
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
