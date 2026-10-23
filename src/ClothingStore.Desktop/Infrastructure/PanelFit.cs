using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace ClothingStore.Desktop.Infrastructure;

/// <summary>
/// Set <c>PanelFit.IsEnabled</c> on a page's <see cref="Grid"/> of side-by-side panels. A panel with a fixed width (a
/// details or form panel) normally never gives up space, so in a narrow window (not maximised, a small screen) the panel
/// on the far side was cut off. Here fixed panels shrink toward their MinWidth when everything doesn't fit, and go back
/// to their width (as set, dragged or remembered) when there is room again.
/// </summary>
public static class PanelFit
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(PanelFit), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    /// <summary>Width each fixed column should have when there is room.</summary>
    private static readonly ConditionalWeakTable<ColumnDefinition, StrongBox<double>> Preferred = new();

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Grid grid) return;
        grid.Loaded -= OnLoaded;
        grid.SizeChanged -= OnSizeChanged;
        grid.RemoveHandler(Thumb.DragCompletedEvent, (DragCompletedEventHandler)OnSplitterReleased);
        if (e.NewValue is not true) return;
        grid.Loaded += OnLoaded;
        grid.SizeChanged += OnSizeChanged;
        grid.AddHandler(Thumb.DragCompletedEvent, (DragCompletedEventHandler)OnSplitterReleased);
    }

    // After LayoutMemory has put back the saved sizes (it does that on Loaded too). Read once: pages are kept between visits,
    // and by a later visit the width may be one this class shrank.
    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var grid = (Grid)sender;
        grid.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            foreach (var column in grid.ColumnDefinitions.Where(c => c.Width.IsAbsolute))
                Preferred.TryAdd(column, new StrongBox<double>(column.Width.Value));
            Fit(grid);
        });
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) Fit((Grid)sender);
    }

    /// <summary>A panel dragged to a new width wants that width from now on.</summary>
    private static void OnSplitterReleased(object sender, DragCompletedEventArgs e)
    {
        var grid = (Grid)sender;
        if (e.OriginalSource is not GridSplitter splitter || splitter.Parent != grid) return;
        foreach (var column in grid.ColumnDefinitions.Where(c => c.Width.IsAbsolute))
            Preferred.AddOrUpdate(column, new StrongBox<double>(column.ActualWidth));
    }

    private static void Fit(Grid grid)
    {
        var width = grid.ActualWidth;
        if (width <= 0) return;

        var fixedColumns = grid.ColumnDefinitions
            .Where(c => c.Width.IsAbsolute && Preferred.TryGetValue(c, out _))
            .ToList();
        if (fixedColumns.Count == 0) return;

        // What everything needs with the fixed panels at full width: flexible panels at their minimum, splitters as they are.
        double needed = 0;
        foreach (var column in grid.ColumnDefinitions)
        {
            if (column.Width.IsStar) needed += column.MinWidth;
            else if (column.Width.IsAuto) needed += column.ActualWidth;
            else needed += Preferred.TryGetValue(column, out var box) ? box.Value : column.Width.Value;
        }

        // Shrink fixed panels (the last one first) only as much as needed, never below their MinWidth.
        var excess = Math.Max(0, needed - width);
        foreach (var column in Enumerable.Reverse(fixedColumns))
        {
            Preferred.TryGetValue(column, out var box);
            var target = Math.Max(column.MinWidth, box!.Value - excess);
            excess -= box.Value - target;
            if (Math.Abs(column.Width.Value - target) > 0.5) column.Width = new GridLength(target);
        }
    }
}
