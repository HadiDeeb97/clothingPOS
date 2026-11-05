using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ClothingStore.Desktop.Infrastructure;

/// <summary>
/// Finger scrolling on touch screens, for every scroll area in every window: tables, lists, pages and dialogs. WPF
/// leaves it off unless each scroll area asks for it, so a finger could only scroll by dragging the scroll bar.
/// <list type="bullet">
/// <item>Drag with one finger to scroll either way; a flick keeps it moving for a moment.</item>
/// <item>The window no longer jumps when a list is dragged past its end (Windows' "boundary feedback").</item>
/// <item>A touch that starts on something that is dragged or typed in (a column edge, a scroll bar, a panel handle, a
/// slider, a text box) works as before instead of scrolling.</item>
/// </list>
/// Mouse, touchpad and wheel are unchanged.
/// </summary>
public static class TouchScrolling
{
    private static bool _enabled;

    public static void Enable()
    {
        if (_enabled) return;
        _enabled = true;
        EventManager.RegisterClassHandler(typeof(ScrollViewer), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded));
        EventManager.RegisterClassHandler(typeof(ScrollViewer), UIElement.PreviewTouchDownEvent, new EventHandler<TouchEventArgs>(OnTouchDown));
        EventManager.RegisterClassHandler(typeof(ScrollViewer), UIElement.PreviewTouchUpEvent, new EventHandler<TouchEventArgs>(OnTouchUp));
        EventManager.RegisterClassHandler(typeof(ScrollViewer), UIElement.LostTouchCaptureEvent, new EventHandler<TouchEventArgs>(OnTouchUp));
        EventManager.RegisterClassHandler(typeof(ScrollViewer), UIElement.ManipulationBoundaryFeedbackEvent,
            new EventHandler<ManipulationBoundaryFeedbackEventArgs>((_, e) => e.Handled = true));
    }

    /// <summary>The panning a scroll area had before a touch on a handle or box turned it off for that touch.</summary>
    private static readonly DependencyProperty PausedProperty = DependencyProperty.RegisterAttached(
        "Paused", typeof(PanningMode?), typeof(TouchScrolling));

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var viewer = (ScrollViewer)sender;
        if (viewer.ReadLocalValue(ScrollViewer.PanningModeProperty) != DependencyProperty.UnsetValue) return; // a screen's own choice
        if (viewer.TemplatedParent is TextBoxBase box)
        {
            // Inside a box: a finger selects and places the cursor as before; only text to read scrolls with a finger.
            if (box.IsReadOnly) viewer.PanningMode = PanningMode.VerticalOnly;
            return;
        }
        viewer.PanningMode = PanningMode.Both;
    }

    private static void OnTouchDown(object? sender, TouchEventArgs e)
    {
        if (sender is not ScrollViewer viewer) return;
        if (viewer.PanningMode == PanningMode.None || e.OriginalSource is not DependencyObject source || !StartsOnHandle(source, viewer)) return;
        viewer.SetValue(PausedProperty, (PanningMode?)viewer.PanningMode);
        viewer.PanningMode = PanningMode.None; // this touch drags the handle (or types), as with a mouse
    }

    private static void OnTouchUp(object? sender, TouchEventArgs e)
    {
        if (sender is not ScrollViewer viewer) return;
        if (viewer.GetValue(PausedProperty) is not PanningMode mode) return;
        viewer.ClearValue(PausedProperty);
        viewer.PanningMode = mode;
    }

    /// <summary>A column edge, scroll bar, panel handle or slider (all <see cref="Thumb"/>s), or a box to type in, under the finger.</summary>
    private static bool StartsOnHandle(DependencyObject source, ScrollViewer viewer)
    {
        for (var d = source; d is not null && d != viewer; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is Thumb or GridSplitter or RangeBase or TextBoxBase { IsReadOnly: false } or PasswordBox) return true;
        return false;
    }
}
