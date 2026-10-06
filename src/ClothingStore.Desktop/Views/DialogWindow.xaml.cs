using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClothingStore.Desktop.Infrastructure;
using ClothingStore.Desktop.ViewModels.Dialogs;

namespace ClothingStore.Desktop.Views;

/// <summary>Hosts any <see cref="IDialogViewModel"/>; the view is picked by DataTemplate.</summary>
public partial class DialogWindow : Window
{
    private readonly IDialogViewModel _viewModel;

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
        Loaded += async (_, _) =>
        {
            MaxHeight = SystemParameters.WorkArea.Height - 40;
            MaxWidth = SystemParameters.WorkArea.Width - 40;
            MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            if (_viewModel is DialogViewModelBase dialog) await dialog.OnOpenedAsync();
        };
    }

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
