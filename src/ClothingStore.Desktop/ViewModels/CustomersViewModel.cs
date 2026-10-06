using ClothingStore.Core.Entities;
using ClothingStore.Core.Security;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Converters;
using ClothingStore.Desktop.Infrastructure;
using ClothingStore.Desktop.Services;
using ClothingStore.Desktop.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClothingStore.Core.Localization;

namespace ClothingStore.Desktop.ViewModels;

public sealed partial class CustomersViewModel(IDialogService dialogs, CustomerService customers, Session session)
    : ViewModelBase(dialogs), IPageViewModel
{
    public string Title => Loc.T("Nav.Customers");
    public bool CanAdjustCredit => session.Can(Permission.AdjustStoreCredit);

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial bool IncludeInactive { get; set; }

    [ObservableProperty]
    public partial List<Customer> Customers { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(AdjustCreditCommand))]
    public partial Customer? SelectedCustomer { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LifetimeSpend), nameof(VisitCount))]
    public partial List<Sale> History { get; set; } = [];

    public decimal LifetimeSpend => History.Where(s => s.Status == Core.SaleStatus.Completed).Sum(s => s.Total);
    public int VisitCount => History.Count(s => s.Status == Core.SaleStatus.Completed);

    public Task OnNavigatedToAsync() => SearchAsync();

    private readonly LatestSearch _search = new();

    partial void OnSearchTextChanged(string value) => _ = SearchAsync(immediately: false);
    partial void OnIncludeInactiveChanged(bool value) => _ = SearchAsync();

    async partial void OnSelectedCustomerChanged(Customer? value)
    {
        try
        {
            History = value is null ? [] : await customers.GetPurchaseHistoryAsync(value.Id);
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Customers.HistoryFailed"), ex);
        }
    }

    [RelayCommand]
    private async Task SearchAsync(bool immediately = true)
    {
        var selectedId = SelectedCustomer?.Id;
        var text = SearchText;
        var includeInactive = IncludeInactive;
        try
        {
            Func<CancellationToken, Task<List<Customer>>> load = ct => customers.SearchAsync(text, includeInactive, max: 500, ct);
            Action<List<Customer>> apply = found =>
            {
                Customers = found;
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == selectedId) ?? Customers.FirstOrDefault();
            };
            await (immediately ? _search.RunNowAsync(load, apply) : _search.RunAsync(load, apply));
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Customers.SearchFailed"), ex);
        }
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        var editor = new CustomerEditorViewModel(Dialogs, customers);
        if (!Dialogs.ShowDialog(editor)) return;
        await SearchAsync();
        SelectedCustomer = Customers.FirstOrDefault(c => c.Id == editor.Saved?.Id) ?? SelectedCustomer;
    }

    private bool HasSelection() => SelectedCustomer is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedCustomer is null) return;
        if (Dialogs.ShowDialog(new CustomerEditorViewModel(Dialogs, customers, SelectedCustomer))) await SearchAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task AdjustCreditAsync()
    {
        if (SelectedCustomer is not { } customer) return;
        if (!CanAdjustCredit)
        {
            Dialogs.Warning(Loc.T("Customers.OnlyManagersCredit"));
            return;
        }
        var amount = Dialogs.PromptDecimal(Loc.T("Customers.AdjustCredit"),
            Loc.T("Customers.AdjustCreditPrompt", customer.FullName, CurrencyFormat.Format(customer.StoreCredit)));
        if (amount is null or 0) return;
        if (await RunAsync(() => customers.AdjustStoreCreditAsync(customer.Id, amount.Value)))
        {
            Dialogs.Toast(Loc.T("Customers.CreditAdjusted", customer.FullName));
            await SearchAsync();
        }
    }

    [RelayCommand]
    private void Export()
    {
        var path = Dialogs.SaveFile(Loc.T("Customers.ExportTitle"), Loc.T("Common.CsvFilter"), "customers.csv");
        if (path is null) return;
        try
        {
            CsvExporter.Write(path,
                [Loc.T("Customers.FirstName"), Loc.T("Customers.LastName"), Loc.T("Common.Phone"), Loc.T("Common.Email"),
                    Loc.T("Common.Points"), Loc.T("Customers.StoreCredit"), Loc.T("Customers.Since"), Loc.T("Common.Active")],
                Customers.Select(c => new object?[] { c.FirstName, c.LastName, c.Phone, c.Email, c.LoyaltyPoints, c.StoreCredit, c.CreatedAt, c.IsActive }));
            Dialogs.Toast(Loc.T("Common.Exported", Customers.Count));
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Common.ExportFailed"), ex);
        }
    }
}
