using System.Globalization;
using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Security;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Infrastructure;
using ClothingStore.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClothingStore.Core.Localization;

namespace ClothingStore.Desktop.ViewModels.Dialogs;

public enum PromptKind { Text, Decimal, Integer }

public sealed partial class PromptViewModel(IDialogService dialogs, string title, string message, string initialValue, PromptKind kind)
    : DialogViewModelBase(dialogs)
{
    public override string Title { get; } = title;
    public string Message { get; } = message;

    [ObservableProperty]
    public partial string Value { get; set; } = initialValue;

    public decimal DecimalValue { get; private set; }

    [RelayCommand]
    private void Ok()
    {
        if (kind == PromptKind.Text)
        {
            if (string.IsNullOrWhiteSpace(Value)) return;
            Close(true);
            return;
        }

        if (!decimal.TryParse(Value, NumberStyles.Number, CultureInfo.CurrentCulture, out var number) &&
            !decimal.TryParse(Value, NumberStyles.Number, CultureInfo.InvariantCulture, out number))
        {
            Dialogs.Warning(Loc.T("Prompt.InvalidNumber"));
            return;
        }
        if (kind == PromptKind.Integer && number != Math.Truncate(number))
        {
            Dialogs.Warning(Loc.T("Prompt.InvalidWholeNumber"));
            return;
        }
        DecimalValue = number;
        Close(true);
    }

    [RelayCommand]
    private void Cancel() => Close(false);
}

public sealed partial class ChangePasswordViewModel(IDialogService dialogs, UserService users, Session session, bool forced)
    : DialogViewModelBase(dialogs)
{
    public override string Title => Loc.T(IsForced ? "Password.ChooseNew" : "Shell.ChangePassword");
    public bool IsForced { get; } = forced;

    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (NewPassword != ConfirmPassword) throw new BusinessRuleException(Loc.T("Password.Mismatch"));
        await users.ChangePasswordAsync(session.User.Id, CurrentPassword, NewPassword);
        session.User.MustChangePassword = false;
        Close(true);
    });

    [RelayCommand]
    private void Cancel() => Close(false);
}

/// <summary>Asks a manager to enter their credentials to authorise an action.</summary>
public sealed partial class ManagerApprovalViewModel(IDialogService dialogs, UserService users, string reason, Permission permission)
    : DialogViewModelBase(dialogs)
{
    public override string Title => Loc.T("Approval.Title");
    public string Reason { get; } = reason;

    [ObservableProperty]
    public partial string Username { get; set; } = "";

    public string Password { get; set; } = "";

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public User? ApprovedBy { get; private set; }

    [RelayCommand]
    private Task ApproveAsync() => RunAsync(async () =>
    {
        ApprovedBy = await users.AuthorizeAsync(Username, Password, permission);
        if (ApprovedBy is null)
        {
            ErrorMessage = Loc.T("Approval.Invalid");
            return;
        }
        Close(true);
    });

    [RelayCommand]
    private void Cancel() => Close(false);
}

/// <summary>Shows monospaced text (receipt, Z report, PO) with a Print button.</summary>
public sealed partial class TextPreviewViewModel(IDialogService dialogs, PrintService print, string title, IReadOnlyList<string> lines, string? closeText = null)
    : DialogViewModelBase(dialogs)
{
    public override string Title { get; } = title;
    public string Text { get; } = string.Join(Environment.NewLine, lines);
    public string CloseText { get; } = closeText ?? Loc.T("Common.Close");

    [RelayCommand]
    private void Print()
    {
        try
        {
            print.PrintText(lines, Title);
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Common.PrintFailed"), ex);
        }
    }

    [RelayCommand]
    private void Done() => Close(true);
}

public sealed partial class CustomerEditorViewModel : DialogViewModelBase
{
    private readonly CustomerService _customers;

    public CustomerEditorViewModel(IDialogService dialogs, CustomerService customers, Customer? existing = null) : base(dialogs)
    {
        _customers = customers;
        Customer = existing is null
            ? new Customer()
            : new Customer
            {
                Id = existing.Id, FirstName = existing.FirstName, LastName = existing.LastName, Phone = existing.Phone,
                Email = existing.Email, Birthday = existing.Birthday, Notes = existing.Notes, IsActive = existing.IsActive,
                LoyaltyPoints = existing.LoyaltyPoints, StoreCredit = existing.StoreCredit,
            };
    }

    public override string Title => Loc.T(Customer.Id == 0 ? "Customers.New" : "Customers.Edit");
    public Customer Customer { get; }
    public Customer? Saved { get; private set; }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        Saved = await _customers.SaveAsync(Customer);
        Close(true);
    });

    [RelayCommand]
    private void Cancel() => Close(false);
}

public sealed partial class CustomerPickerViewModel(IDialogService dialogs, CustomerService customers) : DialogViewModelBase(dialogs)
{
    public override string Title => Loc.T("Customers.Select");

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial List<Customer> Results { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SelectCommand))]
    public partial Customer? SelectedCustomer { get; set; }

    public Customer? Selected { get; private set; }

    private readonly LatestSearch _search = new();

    public override Task OnOpenedAsync() => SearchAsync();

    partial void OnSearchTextChanged(string value) => _ = SearchAsync(immediately: false);

    [RelayCommand]
    private async Task SearchAsync() => await SearchAsync(immediately: true);

    private async Task SearchAsync(bool immediately)
    {
        var text = SearchText;
        try
        {
            Func<CancellationToken, Task<List<Customer>>> load = ct => customers.SearchAsync(text, max: 100, ct: ct);
            Action<List<Customer>> apply = found =>
            {
                Results = found;
                SelectedCustomer = Results.FirstOrDefault();
            };
            await (immediately ? _search.RunNowAsync(load, apply) : _search.RunAsync(load, apply));
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Customers.SearchFailed"), ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSelect))]
    private void Select()
    {
        Selected = SelectedCustomer;
        Close(true);
    }

    private bool CanSelect() => SelectedCustomer is not null;

    [RelayCommand]
    private void New()
    {
        var editor = new CustomerEditorViewModel(Dialogs, customers, new Customer { Phone = LooksLikePhone(SearchText) ? SearchText.Trim() : null });
        if (!Dialogs.ShowDialog(editor) || editor.Saved is null) return;
        Selected = editor.Saved;
        Close(true);
    }

    [RelayCommand]
    private void Cancel() => Close(false);

    private static bool LooksLikePhone(string text) => text.Length > 0 && text.All(c => char.IsDigit(c) || c is '+' or '-' or ' ');
}

public sealed partial class HeldSalesViewModel(IDialogService dialogs, SalesService sales) : DialogViewModelBase(dialogs)
{
    public override string Title => Loc.T("Held.Title");

    [ObservableProperty]
    public partial List<HeldSale> Items { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand), nameof(DeleteCommand))]
    public partial HeldSale? SelectedItem { get; set; }

    public HeldSale? Chosen { get; private set; }

    public override async Task OnOpenedAsync()
    {
        Items = await sales.GetHeldAsync();
        SelectedItem = Items.FirstOrDefault();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Resume()
    {
        Chosen = SelectedItem;
        Close(true);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task DeleteAsync() => RunAsync(async () =>
    {
        if (SelectedItem is null || !Dialogs.Confirm(Loc.T("Held.DiscardConfirm", SelectedItem.Label))) return;
        await sales.DeleteHeldAsync(SelectedItem.Id);
        await OnOpenedAsync();
    });

    private bool HasSelection() => SelectedItem is not null;

    [RelayCommand]
    private void Cancel() => Close(false);
}
