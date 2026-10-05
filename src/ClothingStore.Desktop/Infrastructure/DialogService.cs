using System.Windows;
using ClothingStore.Desktop.ViewModels.Dialogs;
using ClothingStore.Desktop.Views;
using Microsoft.Win32;

namespace ClothingStore.Desktop.Infrastructure;

public interface IDialogService
{
    void Info(string message, string title = "Information");
    void Warning(string message, string title = "Please check");
    void Error(string message, Exception? ex = null);
    bool Confirm(string message, string title = "Please confirm");
    bool ShowDialog(IDialogViewModel viewModel);
    string? Prompt(string title, string message, string? initialValue = null);
    decimal? PromptDecimal(string title, string message, decimal? initialValue = null);
    int? PromptInt(string title, string message, int? initialValue = null);
    string? SaveFile(string title, string filter, string defaultFileName);
}

public sealed class DialogService : IDialogService
{
    private static Window? Owner =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? Application.Current.MainWindow;

    public void Info(string message, string title = "Information") => Show(message, title, MessageBoxImage.Information);

    public void Warning(string message, string title = "Please check") => Show(message, title, MessageBoxImage.Warning);

    public void Error(string message, Exception? ex = null)
    {
        var text = ex is null ? message : $"{message}\n\n{ex.GetBaseException().Message}";
        Show(text, "Error", MessageBoxImage.Error);
    }

    public bool Confirm(string message, string title = "Please confirm")
    {
        var owner = Owner;
        var result = owner is null
            ? MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    public bool ShowDialog(IDialogViewModel viewModel)
    {
        var window = new DialogWindow(viewModel);
        var owner = Owner;
        if (owner is not null && owner != window) window.Owner = owner;
        else window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return window.ShowDialog() == true;
    }

    public string? Prompt(string title, string message, string? initialValue = null)
    {
        var vm = new PromptViewModel(this, title, message, initialValue ?? "", PromptKind.Text);
        return ShowDialog(vm) ? vm.Value : null;
    }

    public decimal? PromptDecimal(string title, string message, decimal? initialValue = null)
    {
        var vm = new PromptViewModel(this, title, message, initialValue?.ToString("0.##") ?? "", PromptKind.Decimal);
        return ShowDialog(vm) ? vm.DecimalValue : null;
    }

    public int? PromptInt(string title, string message, int? initialValue = null)
    {
        var vm = new PromptViewModel(this, title, message, initialValue?.ToString() ?? "", PromptKind.Integer);
        return ShowDialog(vm) ? (int?)vm.DecimalValue : null;
    }

    public string? SaveFile(string title, string filter, string defaultFileName)
    {
        var dialog = new SaveFileDialog { Title = title, Filter = filter, FileName = defaultFileName, AddExtension = true };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    private static void Show(string message, string title, MessageBoxImage image)
    {
        var owner = Owner;
        if (owner is null) MessageBox.Show(message, title, MessageBoxButton.OK, image);
        else MessageBox.Show(owner, message, title, MessageBoxButton.OK, image);
    }
}
