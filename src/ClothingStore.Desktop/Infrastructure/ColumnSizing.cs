using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ClothingStore.Desktop.Infrastructure;

/// <summary>
/// Table columns that fit the screen until the user sizes them, and then go exactly where they are dragged.
/// <list type="bullet">
/// <item>With no sizes of the user's (first use, or after "Reset panel sizes"), columns that come out wider than the
/// table share its width instead, in proportion to their natural widths, so every column is on screen.</item>
/// <item>Dragging a column's edge fixes every column at its current width first, so widening one never squeezes the
/// others (a scroll bar appears instead), and lets the dragged columns go down to <see cref="Floor"/>.</item>
/// </list>
/// </summary>
public static class ColumnSizing
{
    /// <summary>The narrowest a column can be dragged: room for a few letters and the drag handle.</summary>
    public const double Floor = 28;

    /// <summary>The narrowest a column gets when columns share the table's width to fit the screen.</summary>
    private const double FitFloor = 48;

    public static readonly DependencyProperty FreeResizeProperty = DependencyProperty.RegisterAttached(
        "FreeResize", typeof(bool), typeof(ColumnSizing), new PropertyMetadata(false, OnChanged));

    public static bool GetFreeResize(DependencyObject d) => (bool)d.GetValue(FreeResizeProperty);
    public static void SetFreeResize(DependencyObject d, bool value) => d.SetValue(FreeResizeProperty, value);

    /// <summary>The columns are the user's own sizes (dragged now, or remembered): they are no longer fitted to the screen.</summary>
    private static readonly DependencyProperty UserSizedProperty = DependencyProperty.RegisterAttached(
        "UserSized", typeof(bool), typeof(ColumnSizing));

    public static void MarkUserSized(DataGrid grid) => grid.SetValue(UserSizedProperty, true);

    /// <summary>A column the user sized: its own minimum no longer applies.</summary>
    public static void Release(DataGridColumn column)
    {
        if (column.CanUserResize && column.MinWidth > Floor) column.MinWidth = Floor;
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid) return;
        grid.RemoveHandler(Thumb.DragStartedEvent, (DragStartedEventHandler)OnDragStarted);
        grid.Loaded -= OnLoaded;
        grid.Unloaded -= OnUnloaded;
        if (!(bool)e.NewValue) return;
        grid.AddHandler(Thumb.DragStartedEvent, (DragStartedEventHandler)OnDragStarted);
        grid.Loaded += OnLoaded;
        grid.Unloaded += OnUnloaded;
    }

    // ---- Fitting the screen ------------------------------------------------------------------------

    private static readonly DependencyProperty WatcherProperty = DependencyProperty.RegisterAttached(
        "Watcher", typeof(EventHandler), typeof(ColumnSizing));

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var grid = (DataGrid)sender;
        StopWatching(grid);
        // Checked after each layout pass while the table fills (columns fitting their content grow with the rows).
        EventHandler watcher = (_, _) => FitIfWider(grid);
        grid.SetValue(WatcherProperty, watcher);
        grid.LayoutUpdated += watcher;
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e) => StopWatching((DataGrid)sender);

    private static void StopWatching(DataGrid grid)
    {
        if (grid.GetValue(WatcherProperty) is EventHandler watcher) grid.LayoutUpdated -= watcher;
        grid.ClearValue(WatcherProperty);
    }

    private static void FitIfWider(DataGrid grid)
    {
        if ((bool)grid.GetValue(UserSizedProperty))
        {
            StopWatching(grid);
            return;
        }
        if (FindChild<ScrollViewer>(grid) is not { ViewportWidth: > 0 } scroller || scroller.ExtentWidth <= scroller.ViewportWidth + 1) return;

        // Wider than the table: the columns share its width, each in proportion to its natural width.
        var sizable = grid.Columns.Where(c => c.Visibility == Visibility.Visible && c.CanUserResize && c.ActualWidth > 0).ToList();
        if (sizable.Count == 0) return;
        foreach (var column in sizable)
        {
            column.MinWidth = Math.Min(column.MinWidth, FitFloor);
            column.Width = new DataGridLength(column.ActualWidth, DataGridLengthUnitType.Star);
        }
        StopWatching(grid); // shares of the width always fit, however the rows grow
    }

    // ---- Dragging --------------------------------------------------------------------------------

    private static void OnDragStarted(object sender, DragStartedEventArgs e)
    {
        if (sender is not DataGrid grid || e.OriginalSource is not Thumb thumb || FindAncestor<DataGridColumnHeader>(thumb) is not { Column: not null }) return;
        MarkUserSized(grid);
        StopWatching(grid);
        // Every column keeps the width it has now, so the dragged one takes no room from the others, and any of them
        // can then be dragged as narrow as the user likes.
        foreach (var column in grid.Columns.Where(c => c.Visibility == Visibility.Visible && c.CanUserResize && c.ActualWidth > 0))
        {
            Release(column);
            if (!column.Width.IsAbsolute) column.Width = new DataGridLength(column.ActualWidth);
        }
    }

    private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        for (var d = start; d is not null; d = VisualTreeHelper.GetParent(d))
            if (d is T found) return found;
        return null;
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            if (FindChild<T>(child) is { } deeper) return deeper;
        }
        return null;
    }
}
