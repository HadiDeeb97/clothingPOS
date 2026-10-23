using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ClothingStore.Desktop.ViewModels;

namespace ClothingStore.Desktop.Views;

public partial class SalesView : UserControl
{
    public SalesView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is SalesViewModel old)
            {
                old.FocusSearchRequested -= OnFocusSearchRequested;
                old.PropertyChanged -= OnViewModelPropertyChanged;
            }
            if (e.NewValue is SalesViewModel vm)
            {
                vm.FocusSearchRequested += OnFocusSearchRequested;
                vm.PropertyChanged += OnViewModelPropertyChanged;
            }
        };
        Loaded += (_, _) =>
        {
            // The screen is kept between visits: listen again after being taken off screen (Unloaded stops listening).
            if (DataContext is SalesViewModel current)
            {
                current.FocusSearchRequested -= OnFocusSearchRequested;
                current.PropertyChanged -= OnViewModelPropertyChanged;
                current.FocusSearchRequested += OnFocusSearchRequested;
                current.PropertyChanged += OnViewModelPropertyChanged;
            }
            FocusSearch();
            // After the saved panel sizes are restored.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (!_widthsRead)
                {
                    if (BrowseColumn.Width is { IsAbsolute: true, Value: >= 200 } browse) _browseWidth = browse;
                    if (PanelColumn.Width is { IsAbsolute: true } panel && panel.Value >= PanelColumn.MinWidth) _panelWidth = panel;
                    _widthsRead = true;
                }
                FitPanels();
            });
        };
        RegisterGrid.SizeChanged += (_, e) =>
        {
            if (e.WidthChanged && _widthsRead) FitPanels(); // not before the saved panel sizes are known
        };
        BrowseSplitter.DragCompleted += (_, _) =>
        {
            if (BrowseColumn.ActualWidth >= 200) _browseWidth = new GridLength(BrowseColumn.ActualWidth);
        };
        PanelSplitter.DragCompleted += (_, _) =>
        {
            _panelWidth = new GridLength(PanelColumn.ActualWidth);
            FitPanels();
        };
        Unloaded += (_, _) =>
        {
            if (DataContext is SalesViewModel vm)
            {
                vm.FocusSearchRequested -= OnFocusSearchRequested;
                vm.PropertyChanged -= OnViewModelPropertyChanged;
            }
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F2)
            {
                FocusSearch();
                e.Handled = true;
            }
        };
    }

    private SalesViewModel? ViewModel => DataContext as SalesViewModel;

    private const double BrowseMinWidth = 220;

    /// <summary>Panel widths as the cashier left them (dragged or remembered), used whenever there is room.</summary>
    private GridLength _browseWidth = new(330), _panelWidth = new(380);
    private bool _widthsRead;

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SalesViewModel.IsBrowseVisible)) FitPanels();
    }

    /// <summary>
    /// Shows the product list when the cashier wants it and it fits beside the cart and the payment panel. In a narrower
    /// window (not maximised, a small screen) it steps aside by itself, and comes back when there is room again, so the
    /// totals and the Pay button are never cut off. Hidden, it gives its whole column to the cart.
    /// </summary>
    private void FitPanels()
    {
        var total = RegisterGrid.ActualWidth;
        if (total <= 0) return;
        // The payment panel gives up width (down to its minimum) before the cart goes below its own.
        var panel = Math.Max(PanelColumn.MinWidth, Math.Min(_panelWidth.Value, total - CartColumn.MinWidth - PanelSplitter.Width));
        PanelColumn.Width = new GridLength(panel);
        var room = total - CartColumn.MinWidth - panel - BrowseSplitter.Width - PanelSplitter.Width;
        var show = (ViewModel?.IsBrowseVisible ?? true) && room >= BrowseMinWidth;
        if (show)
        {
            BrowseColumn.MinWidth = BrowseMinWidth;
            BrowseColumn.Width = new GridLength(Math.Min(Math.Max(_browseWidth.Value, BrowseMinWidth), room));
        }
        else
        {
            BrowseColumn.MinWidth = 0;
            BrowseColumn.Width = new GridLength(0);
        }
        BrowsePanel.Visibility = BrowseSplitter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnFocusSearchRequested(object? sender, EventArgs e) => FocusSearch();

    private void FocusSearch() =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
        });

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        switch (e.Key)
        {
            case Key.Down when vm.IsSearchOpen:
                ResultsGrid.Focus();
                if (ResultsGrid.SelectedIndex < 0 && ResultsGrid.Items.Count > 0) ResultsGrid.SelectedIndex = 0;
                e.Handled = true;
                break;
            case Key.Escape when vm.IsSearchOpen:
                vm.CloseSearchCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Add or Key.OemPlus when vm.SearchText.Length == 0:
                vm.IncreaseCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Subtract or Key.OemMinus when vm.SearchText.Length == 0:
                vm.DecreaseCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Delete when vm.SearchText.Length == 0:
                vm.RemoveItemCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnResultsKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if (e.Key == Key.Enter)
        {
            vm.AddResultCommand.Execute(ResultsGrid.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CloseSearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e) =>
        ViewModel?.AddResultCommand.Execute(ResultsGrid.SelectedItem);

    private void OnCartKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if (e.Key == Key.Delete)
        {
            vm.RemoveItemCommand.Execute(null);
            e.Handled = true;
        }
    }
}
