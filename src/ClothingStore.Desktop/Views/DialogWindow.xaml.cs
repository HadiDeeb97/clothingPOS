using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ClothingStore.Desktop.Infrastructure;
using ClothingStore.Desktop.ViewModels.Dialogs;

namespace ClothingStore.Desktop.Views;

/// <summary>Hosts any <see cref="IDialogViewModel"/>; the view is picked by DataTemplate.</summary>
public partial class DialogWindow : Window
{
    private readonly IDialogViewModel _viewModel;

    /// <summary>Largest size while the window still sizes itself to its content (a form); lifted when maximised.</summary>
    private readonly double _maxWidth, _maxHeight;

    private bool _remaximizing;

    public DialogWindow(IDialogViewModel viewModel)
    {
        InitializeComponent();
        WindowAppearance.Apply(this);
        if (viewModel is MessageDialogViewModel) ResizeMode = ResizeMode.NoResize;
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= OnCloseRequested;
        PreviewKeyDown += OnPreviewKeyDown;
        // Set before the first layout so SizeToContent never makes a dialog taller or wider than the screen: content
        // shrinks to fit (lists and text areas scroll) instead of the bottom buttons ending up off-screen.
        MaxHeight = _maxHeight = SystemParameters.WorkArea.Height - 40;
        MaxWidth = _maxWidth = SystemParameters.WorkArea.Width - 40;
        SourceInitialized += (_, _) => PrepareWindowButtons();
        StateChanged += OnStateChanged;
        Loaded += async (_, _) =>
        {
            FitViewToScreen();
            FollowWindowSize();
            MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            if (_viewModel is DialogViewModelBase dialog) await dialog.OnOpenedAsync();
        };
    }

    /// <summary>
    /// Dialog views give a preferred size (Width/Height). On a small screen (a 1366×768 laptop) that would push the
    /// buttons off the bottom, so shrink the view to what fits; its lists and text areas scroll instead.
    /// </summary>
    private void FitViewToScreen()
    {
        if (View is not { } view) return;
        var chromeHeight = SystemParameters.WindowCaptionHeight + SystemParameters.ResizeFrameHorizontalBorderHeight * 2 + 8;
        var chromeWidth = SystemParameters.ResizeFrameVerticalBorderWidth * 2 + 8;
        var maxWidth = MaxWidth - Host.Margin.Left - Host.Margin.Right - chromeWidth;
        var maxHeight = MaxHeight - Host.Margin.Top - Host.Margin.Bottom - chromeHeight;
        if (!double.IsNaN(view.Width) && view.Width > maxWidth) view.Width = Math.Max(300, maxWidth);
        if (!double.IsNaN(view.Height) && view.Height > maxHeight) view.Height = Math.Max(240, maxHeight);
    }

    private FrameworkElement? View =>
        VisualTreeHelper.GetChildrenCount(Host) > 0 ? VisualTreeHelper.GetChild(Host, 0) as FrameworkElement : null;

    /// <summary>
    /// A view's Width/Height is only the size the dialog opens at; afterwards the window decides. Lists and editors (views
    /// that give a height) fill the window when it is resized or maximised, with their tables scrolling inside. Forms keep
    /// their width and stay centred, and still grow with their content (a message appearing) as before.
    /// </summary>
    private void FollowWindowSize()
    {
        if (View is not { } view || double.IsNaN(view.Width)) return; // sizes itself to its content (message, preview)
        UpdateLayout(); // take in FitViewToScreen's shrinking before reading sizes

        if (double.IsNaN(view.Height))
        {
            view.HorizontalAlignment = HorizontalAlignment.Center;
            MinWidth = Math.Max(MinWidth, ActualWidth); // narrower would cut the form off: it doesn't scroll sideways
            return;
        }

        // Window border, title bar and margins around the view: what the window needs on top of the view itself.
        var chromeWidth = Math.Max(0, ActualWidth - view.ActualWidth);
        var chromeHeight = Math.Max(0, ActualHeight - view.ActualHeight);
        var minViewWidth = Math.Min(view.ActualWidth, Math.Max(320, Math.Round(view.Width * 0.75)));
        var minViewHeight = Math.Min(view.ActualHeight, Math.Max(240, Math.Round(view.Height * 0.75)));

        // Keep the size it opened at, then let the view follow the window.
        var (width, height) = (ActualWidth, ActualHeight);
        SizeToContent = SizeToContent.Manual;
        Width = width;
        Height = height;
        view.Width = double.NaN;
        view.Height = double.NaN;
        Scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled; // the view gets the window's height
        MinWidth = Math.Max(MinWidth, minViewWidth + chromeWidth);
        MinHeight = Math.Max(MinHeight, minViewHeight + chromeHeight);
        // It no longer sizes itself to its content, so it needs no cap: maximising fills the screen.
        MaxWidth = double.PositiveInfinity;
        MaxHeight = double.PositiveInfinity;
    }

    /// <summary>
    /// Dialogs maximise but don't minimise: the window behind waits for the dialog, so a minimised dialog would leave the
    /// POS looking frozen. Minimising the main window still takes its open dialog with it.
    /// </summary>
    private void PrepareWindowButtons()
    {
        var handle = new WindowInteropHelper(this).Handle;
        SetWindowLong(handle, GwlStyle, GetWindowLong(handle, GwlStyle) & ~WsMinimizeBox);
        HwndSource.FromHwnd(handle)?.AddHook(OnWindowMessage);
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmSysCommand) return IntPtr.Zero;
        switch ((int)wParam & 0xFFF0)
        {
            case ScMinimize:
                handled = true; // Win+Down or the window menu
                break;
            case ScMaximize:
                // Lift the cap before Windows works out the maximised size, so it covers the whole screen.
                MaxWidth = double.PositiveInfinity;
                MaxHeight = double.PositiveInfinity;
                break;
        }
        return IntPtr.Zero;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        switch (WindowState)
        {
            case WindowState.Minimized:
                WindowState = WindowState.Normal;
                break;
            case WindowState.Maximized when !double.IsPositiveInfinity(MaxWidth) || !double.IsPositiveInfinity(MaxHeight):
                // Maximised some other way (dragged to the top of the screen) while still capped: lift the cap and redo it.
                MaxWidth = double.PositiveInfinity;
                MaxHeight = double.PositiveInfinity;
                _remaximizing = true;
                Dispatcher.BeginInvoke(() =>
                {
                    WindowState = WindowState.Normal;
                    WindowState = WindowState.Maximized;
                    _remaximizing = false;
                });
                break;
            case WindowState.Normal when SizeToContent != SizeToContent.Manual && !_remaximizing:
                // Back to a form sizing itself to its content: never taller or wider than the screen.
                MaxWidth = _maxWidth;
                MaxHeight = _maxHeight;
                break;
        }
    }

    private const int GwlStyle = -16;
    private const int WsMinimizeBox = 0x00020000;
    private const int WmSysCommand = 0x0112;
    private const int ScMinimize = 0xF020;
    private const int ScMaximize = 0xF030;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var focused = Keyboard.FocusedElement as DependencyObject;
        switch (e.Key)
        {
            case Key.Enter:
                // Enter clicks the default button without moving focus, so push what was typed in the focused box
                // first; otherwise a field bound on lost focus (a price, say) would be saved with its old value.
                if (focused is TextBox { AcceptsReturn: false } box)
                    box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                break;

            case Key.Escape:
                // Let an open drop-down close itself, and don't abandon a save that is still running
                // (it would finish anyway, but the caller would be told it was cancelled).
                if (IsDropDownOpen(focused) || _viewModel is ViewModelBase { IsBusy: true }) return;
                e.Handled = true;
                Close();
                break;
        }
    }

    private static bool IsDropDownOpen(DependencyObject? element)
    {
        for (var d = element; d is not null; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
        {
            if (d is ComboBox { IsDropDownOpen: true } or DatePicker { IsDropDownOpen: true }) return true;
            if (d is Window) break;
        }
        // Items of an open drop-down live in a popup, outside the window's tree.
        return element is ComboBoxItem || element is FrameworkElement { TemplatedParent: ComboBox { IsDropDownOpen: true } };
    }

    private void OnCloseRequested(object? sender, bool result)
    {
        DialogResult = result;
    }
}
