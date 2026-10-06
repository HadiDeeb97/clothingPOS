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
            FocusSearch();
            // After the saved panel sizes are restored.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => ApplyBrowseVisibility(ViewModel?.IsBrowseVisible ?? true));
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

    private static readonly GridLength DefaultBrowseWidth = new(330);
    private GridLength _browseWidth = DefaultBrowseWidth;

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SalesViewModel.IsBrowseVisible)) ApplyBrowseVisibility(ViewModel?.IsBrowseVisible ?? true);
    }

    /// <summary>A hidden product list gives its whole column back to the cart; showing it restores its last width.</summary>
    private void ApplyBrowseVisibility(bool visible)
    {
        if (visible)
        {
            BrowseColumn.Width = _browseWidth.Value >= 200 ? _browseWidth : DefaultBrowseWidth;
            BrowseColumn.MinWidth = 220;
        }
        else
        {
            if (BrowseColumn.ActualWidth >= 200) _browseWidth = BrowseColumn.Width;
            BrowseColumn.MinWidth = 0;
            BrowseColumn.Width = new GridLength(0);
        }
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
