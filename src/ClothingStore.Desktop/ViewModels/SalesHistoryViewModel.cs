using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Localization;
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
    public string Title => Loc.T("Nav.SalesHistory");
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
            Dialogs.Error(Loc.T("History.SearchFailed"), ex);
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
        Dialogs.ShowDialog(new TextPreviewViewModel(Dialogs, print, Loc.T("History.ReceiptTitle", sale.ReceiptNumber),
            ReceiptFormatter.Format(doc, settings.Current.ReceiptWidth)));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task VoidAsync()
    {
        if (SelectedSale is not { } sale) return;
        if (!CanVoid)
        {
            Dialogs.Warning(Loc.T("History.OnlyManagersVoid"));
            return;
        }
        if (sale.Status == SaleStatus.Voided)
        {
            Dialogs.Warning(Loc.T("History.AlreadyVoided"));
            return;
        }
        var reason = Dialogs.Prompt(Loc.T("History.VoidSale"), Loc.T("History.VoidPrompt", sale.ReceiptNumber));
        if (reason is null) return;

        if (await RunAsync(() => sales.VoidSaleAsync(sale.Id, session.User.Id, reason)))
        {
            Dialogs.Toast(Loc.T("History.Voided", sale.ReceiptNumber));
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
        var path = Dialogs.SaveFile(Loc.T("History.ExportTitle"), Loc.T("Common.CsvFilter"), $"sales_{From:yyyyMMdd}_{To:yyyyMMdd}.csv");
        if (path is null) return;
        try
        {
            CsvExporter.Write(path,
                [Loc.T("Common.Receipt"), Loc.T("Common.Date"), Loc.T("Common.Status"), Loc.T("Common.Cashier"), Loc.T("Common.Customer"),
                    Loc.T("Common.Items"), Loc.T("Common.Subtotal"), Loc.T("Common.Discount"), Loc.T("Common.Tax"), Loc.T("Common.Total"),
                    Loc.T("Common.Payments")],
                Sales.Select(s => new object?[]
                {
                    s.ReceiptNumber, s.CreatedAt, Loc.EnumText(s.Status), s.User?.FullName, s.Customer?.FullName, s.Lines.Sum(l => l.Quantity),
                    s.Subtotal, s.DiscountTotal, s.TaxTotal, s.Total,
                    string.Join(" + ", s.Payments.Select(p => $"{Loc.EnumText(p.Method)} {p.Amount:0.00}")),
                }));
            Dialogs.Toast(Loc.T("Common.Exported", Sales.Count));
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Common.ExportFailed"), ex);
        }
    }
}
