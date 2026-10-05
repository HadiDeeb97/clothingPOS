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
            if (e.OldValue is SalesViewModel old) old.FocusSearchRequested -= OnFocusSearchRequested;
            if (e.NewValue is SalesViewModel vm) vm.FocusSearchRequested += OnFocusSearchRequested;
        };
        Loaded += (_, _) => FocusSearch();
        Unloaded += (_, _) =>
        {
            if (DataContext is SalesViewModel vm) vm.FocusSearchRequested -= OnFocusSearchRequested;
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
