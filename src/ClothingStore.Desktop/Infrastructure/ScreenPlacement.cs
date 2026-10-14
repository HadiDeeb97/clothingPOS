using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClothingStore.Desktop.Infrastructure;

/// <summary>Puts a window full screen on a given monitor without ever activating it (the register keeps the keyboard).</summary>
public static class ScreenPlacement
{
    public static void FullScreenOn(Window window, ScreenInfo screen)
    {
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
        window.ShowInTaskbar = false;
        window.ShowActivated = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            // Clicking it never takes focus from the register, and it stays out of Alt+Tab.
            SetWindowLong(hwnd, GwlExStyle, GetWindowLong(hwnd, GwlExStyle) | WsExNoActivate | WsExToolWindow);
            // Monitor coordinates are physical pixels (the app is per-monitor DPI aware), so move it there directly.
            var b = screen.Bounds;
            SetWindowPos(hwnd, IntPtr.Zero, b.X, b.Y, b.Width, b.Height, SwpNoZOrder | SwpNoActivate);
        };
        // Maximised after the move, so it fills that monitor whatever its scaling.
        window.Loaded += (_, _) => window.WindowState = WindowState.Maximized;
    }

    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
