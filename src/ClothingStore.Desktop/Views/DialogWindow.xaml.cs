using System.Windows;
using System.Windows.Input;
using ClothingStore.Desktop.Infrastructure;

namespace ClothingStore.Desktop.Views;

/// <summary>Hosts any <see cref="IDialogViewModel"/>; the view is picked by DataTemplate.</summary>
public partial class DialogWindow : Window
{
    private readonly IDialogViewModel _viewModel;

    public DialogWindow(IDialogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= OnCloseRequested;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
        Loaded += async (_, _) =>
        {
            MaxHeight = SystemParameters.WorkArea.Height - 40;
            MaxWidth = SystemParameters.WorkArea.Width - 40;
            MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            if (_viewModel is DialogViewModelBase dialog) await dialog.OnOpenedAsync();
        };
    }

    private void OnCloseRequested(object? sender, bool result)
    {
        DialogResult = result;
    }
}
