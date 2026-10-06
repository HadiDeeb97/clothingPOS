using ClothingStore.Core;
using ClothingStore.Core.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClothingStore.Desktop.Infrastructure;

public abstract partial class ViewModelBase(IDialogService dialogs) : ObservableObject
{
    protected IDialogService Dialogs { get; } = dialogs;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// Runs an operation with a busy indicator. Business-rule violations are shown as warnings,
    /// anything unexpected as an error — the app never crashes on a failed operation.
    /// </summary>
    protected async Task<bool> RunAsync(Func<Task> action)
    {
        if (IsBusy) return false;
        try
        {
            IsBusy = true;
            await action();
            return true;
        }
        catch (BusinessRuleException ex)
        {
            Dialogs.Warning(ex.Message);
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Common.SomethingWentWrong"), ex);
        }
        finally
        {
            IsBusy = false;
        }
        return false;
    }
}

/// <summary>A screen shown in the main window's content area.</summary>
public interface IPageViewModel
{
    string Title { get; }
    Task OnNavigatedToAsync();
}

/// <summary>Content of a modal <c>DialogWindow</c>.</summary>
public interface IDialogViewModel
{
    string Title { get; }
    event EventHandler<bool>? CloseRequested;
}

public abstract class DialogViewModelBase(IDialogService dialogs) : ViewModelBase(dialogs), IDialogViewModel
{
    public abstract string Title { get; }
    public event EventHandler<bool>? CloseRequested;

    protected void Close(bool result) => CloseRequested?.Invoke(this, result);

    /// <summary>Called once the dialog window has loaded.</summary>
    public virtual Task OnOpenedAsync() => Task.CompletedTask;
}
