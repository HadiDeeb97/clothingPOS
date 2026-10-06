using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Receipts;
using ClothingStore.Core.Security;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Infrastructure;
using ClothingStore.Desktop.Services;
using ClothingStore.Desktop.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClothingStore.Desktop.ViewModels;

public sealed partial class SalesHistoryViewModel(
    IDialogService dialogs, SalesService sales, SettingsService settings, Session session,
    PrintService print, INavigationService navigation) : ViewModelBase(dialogs), IPageViewModel
{
    public string Title => "Sales History";
    public bool CanVoid => session.Can(Permission.VoidSales);
    public bool CanReturn => session.Can(Permission.ProcessReturns);

    [ObservableProperty]
    public partial DateTime From { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial DateTime To { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial bool IncludeVoided { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalSales), nameof(SaleCount))]
    public partial List<Sale> Sales { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReprintCommand), nameof(VoidCommand), nameof(ReturnCommand))]
    public partial Sale? SelectedSale { get; set; }

    public decimal TotalSales => Sales.Where(s => s.Status == SaleStatus.Completed).Sum(s => s.Total);
    public int SaleCount => Sales.Count(s => s.Status == SaleStatus.Completed);

    private readonly LatestSearch _search = new();

    public Task OnNavigatedToAsync() => SearchAsync();

    partial void OnSearchTextChanged(string value) => _ = LoadAsync(immediately: false);
    partial void OnIncludeVoidedChanged(bool value) => _ = SearchAsync();
    partial void OnFromChanged(DateTime value) => _ = LoadAsync(immediately: false);
    partial void OnToChanged(DateTime value) => _ = LoadAsync(immediately: false);

    [RelayCommand]
    private Task SearchAsync() => LoadAsync(immediately: true);

    private async Task LoadAsync(bool immediately)
    {
        var (from, to) = From <= To ? (From.Date, To.Date) : (To.Date, From.Date);
        var text = SearchText;
        var includeVoided = IncludeVoided;
        try
        {
            Func<CancellationToken, Task<List<Sale>>> load = ct => sales.SearchAsync(from, to.AddDays(1), text, includeVoided, ct);
            Action<List<Sale>> apply = found =>
            {
                Sales = found;
                SelectedSale = Sales.FirstOrDefault();
            };
            await (immediately ? _search.RunNowAsync(load, apply) : _search.RunAsync(load, apply));
        }
        catch (Exception ex)
        {
            Dialogs.Error("Sales search failed.", ex);
        }
    }

    [RelayCommand]
    private Task QuickRangeAsync(string range)
    {
        (From, To) = range switch
        {
            "yesterday" => (DateTime.Today.AddDays(-1), DateTime.Today.AddDays(-1)),
            "week" => (DateTime.Today.AddDays(-6), DateTime.Today),
            "month" => (new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today),
            _ => (DateTime.Today, DateTime.Today),
        };
        return SearchAsync();
    }

    private bool HasSelection() => SelectedSale is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Reprint()
    {
        if (SelectedSale is not { } sale) return;
        var doc = ReceiptBuilder.FromSale(sale, settings.Current, isCopy: true);
        Dialogs.ShowDialog(new TextPreviewViewModel(Dialogs, print, $"Receipt {sale.ReceiptNumber}",
            ReceiptFormatter.Format(doc, settings.Current.ReceiptWidth)));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task VoidAsync()
    {
        if (SelectedSale is not { } sale) return;
        if (!CanVoid)
        {
            Dialogs.Warning("Only managers can void sales.");
            return;
        }
        if (sale.Status == SaleStatus.Voided)
        {
            Dialogs.Warning("This sale is already voided.");
            return;
        }
        var reason = Dialogs.Prompt("Void sale", $"Why is sale {sale.ReceiptNumber} being voided?\nStock will be returned and customer balances reversed.");
        if (reason is null) return;

        if (await RunAsync(() => sales.VoidSaleAsync(sale.Id, session.User.Id, reason)))
        {
            Dialogs.Info($"Sale {sale.ReceiptNumber} has been voided.");
            await SearchAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ReturnAsync()
    {
        if (SelectedSale is not { } sale) return;
        await navigation.NavigateToAsync<ReturnsViewModel>(vm => vm.LoadReceiptAsync(sale.ReceiptNumber));
    }

    [RelayCommand]
    private void Export()
    {
        var path = Dialogs.SaveFile("Export sales", "CSV files (*.csv)|*.csv", $"sales_{From:yyyyMMdd}_{To:yyyyMMdd}.csv");
        if (path is null) return;
        try
        {
            CsvExporter.Write(path,
                ["Receipt", "Date", "Status", "Cashier", "Customer", "Items", "Subtotal", "Discount", "Tax", "Total", "Payments"],
                Sales.Select(s => new object?[]
                {
                    s.ReceiptNumber, s.CreatedAt, s.Status, s.User?.FullName, s.Customer?.FullName, s.Lines.Sum(l => l.Quantity),
                    s.Subtotal, s.DiscountTotal, s.TaxTotal, s.Total,
                    string.Join(" + ", s.Payments.Select(p => $"{p.Method} {p.Amount:0.00}")),
                }));
            Dialogs.Info($"Exported {Sales.Count} sales.");
        }
        catch (Exception ex)
        {
            Dialogs.Error("Export failed.", ex);
        }
    }
}
