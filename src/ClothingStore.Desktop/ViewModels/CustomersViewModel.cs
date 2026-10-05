using ClothingStore.Core.Entities;
using ClothingStore.Core.Security;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Converters;
using ClothingStore.Desktop.Infrastructure;
using ClothingStore.Desktop.Services;
using ClothingStore.Desktop.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClothingStore.Desktop.ViewModels;

public sealed partial class CustomersViewModel(IDialogService dialogs, CustomerService customers, Session session)
    : ViewModelBase(dialogs), IPageViewModel
{
    public string Title => "Customers";
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

    partial void OnSearchTextChanged(string value) => _ = SearchAsync();
    partial void OnIncludeInactiveChanged(bool value) => _ = SearchAsync();

    async partial void OnSelectedCustomerChanged(Customer? value)
    {
        try
        {
            History = value is null ? [] : await customers.GetPurchaseHistoryAsync(value.Id);
        }
        catch (Exception ex)
        {
            Dialogs.Error("Could not load purchase history.", ex);
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        var selectedId = SelectedCustomer?.Id;
        try
        {
            Customers = await customers.SearchAsync(SearchText, IncludeInactive, max: 500);
            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == selectedId) ?? Customers.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Dialogs.Error("Customer search failed.", ex);
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
            Dialogs.Warning("Only managers can adjust store credit.");
            return;
        }
        var amount = Dialogs.PromptDecimal("Adjust store credit",
            $"{customer.FullName} has {CurrencyFormat.Format(customer.StoreCredit)}.\nEnter an amount to add (use a negative number to deduct):");
        if (amount is null or 0) return;
        if (await RunAsync(() => customers.AdjustStoreCreditAsync(customer.Id, amount.Value))) await SearchAsync();
    }

    [RelayCommand]
    private void Export()
    {
        var path = Dialogs.SaveFile("Export customers", "CSV files (*.csv)|*.csv", "customers.csv");
        if (path is null) return;
        try
        {
            CsvExporter.Write(path, ["First name", "Last name", "Phone", "Email", "Points", "Store credit", "Since", "Active"],
                Customers.Select(c => new object?[] { c.FirstName, c.LastName, c.Phone, c.Email, c.LoyaltyPoints, c.StoreCredit, c.CreatedAt, c.IsActive }));
            Dialogs.Info($"Exported {Customers.Count} customers.");
        }
        catch (Exception ex)
        {
            Dialogs.Error("Export failed.", ex);
        }
    }
}
