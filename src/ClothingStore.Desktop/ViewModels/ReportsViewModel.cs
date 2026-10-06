using ClothingStore.Core;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Converters;
using ClothingStore.Desktop.Infrastructure;
using ClothingStore.Desktop.Services;
using ClothingStore.Desktop.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClothingStore.Core.Localization;

namespace ClothingStore.Desktop.ViewModels;

public sealed partial class ReportsViewModel(
    IDialogService dialogs, ReportService reports, SettingsService settings, PrintService print)
    : ViewModelBase(dialogs), IPageViewModel
{
    public string Title => Loc.T("Nav.Reports");

    [ObservableProperty]
    public partial DateTime From { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    public partial DateTime To { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial SalesReport? Report { get; set; }

    [ObservableProperty]
    public partial InventoryValuation? Valuation { get; set; }

    public Task OnNavigatedToAsync() => RunReportAsync();

    [RelayCommand]
    private Task RunReportAsync() => RunAsync(async () =>
    {
        if (To < From) (From, To) = (To, From);
        Report = await reports.GetSalesReportAsync(From.Date, To.Date.AddDays(1));
        Valuation = await reports.GetInventoryValuationAsync();
    });

    [RelayCommand]
    private Task QuickRangeAsync(string range)
    {
        var today = DateTime.Today;
        (From, To) = range switch
        {
            "today" => (today, today),
            "yesterday" => (today.AddDays(-1), today.AddDays(-1)),
            "week" => (today.AddDays(-((7 + (int)today.DayOfWeek - 1) % 7)), today),
            "lastmonth" => (new DateTime(today.Year, today.Month, 1).AddMonths(-1), new DateTime(today.Year, today.Month, 1).AddDays(-1)),
            "year" => (new DateTime(today.Year, 1, 1), today),
            _ => (new DateTime(today.Year, today.Month, 1), today),
        };
        return RunReportAsync();
    }

    [RelayCommand]
    private void Export(string section)
    {
        if (Report is null) return;
        var path = Dialogs.SaveFile(Loc.T("Reports.ExportTitle"), Loc.T("Common.CsvFilter"), $"{section}_{From:yyyyMMdd}_{To:yyyyMMdd}.csv");
        if (path is null) return;
        try
        {
            switch (section)
            {
                case "daily":
                    CsvExporter.Write(path, [Loc.T("Common.Date"), Loc.T("Reports.Transactions"), Loc.T("Common.Total")],
                        Report.ByDay.Select(d => new object?[] { d.Date.ToString("yyyy-MM-dd"), d.Transactions, d.Total }));
                    break;
                case "payments":
                    CsvExporter.Write(path, [Loc.T("Reports.Method"), Loc.T("Reports.Count"), Loc.T("Common.Amount")], Report.ByPaymentMethod.Select(x => new object?[] { x.Name, x.Count, x.Amount }));
                    break;
                case "cashiers":
                    CsvExporter.Write(path, [Loc.T("Common.Cashier"), Loc.T("Reports.Transactions"), Loc.T("Common.Items"), Loc.T("Common.Total")], Report.ByCashier.Select(x => new object?[] { x.Name, x.Count, x.Quantity, x.Amount }));
                    break;
                default:
                    var rows = section switch
                    {
                        "categories" => Report.ByCategory,
                        "sizes" => Report.BySize,
                        _ => Report.TopProducts,
                    };
                    CsvExporter.Write(path, [Loc.T("Common.Name"), Loc.T("Reports.Units"), Loc.T("Reports.NetSales")], rows.Select(x => new object?[] { x.Name, x.Quantity, x.Amount }));
                    break;
            }
            Dialogs.Toast(Loc.T("Reports.Exported"));
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Common.ExportFailed"), ex);
        }
    }

    [RelayCommand]
    private void PrintSummary()
    {
        if (Report is not { } r) return;
        string M(decimal v) => CurrencyFormat.Format(v);
        string Row(string l, string v) => $"{l,-28}{v,14}";
        var lines = new List<string>
        {
            settings.Current.StoreName.ToUpperInvariant(),
            Loc.T("Reports.SummaryHeader", r.From, r.To.AddDays(-1)),
            new('-', 42),
            Row(Loc.T("Reports.Transactions"), r.Transactions.ToString()),
            Row(Loc.T("Reports.ItemsSold"), r.ItemsSold.ToString()),
            Row(Loc.T("Reports.GrossSales"), M(r.GrossSales)),
            Row(Loc.T("Common.Discounts"), M(-r.Discounts)),
            Row(Loc.T("Reports.TaxCollected"), M(r.Tax)),
            Row(Loc.T("Reports.TotalSalesInclTax"), M(r.TotalSales)),
            Row(Loc.T("Shift.Refunds"), M(-r.Refunds)),
            Row(Loc.T("Reports.NetRevenueExTax"), M(r.NetRevenue)),
            Row(Loc.T("Reports.CostOfGoods"), M(r.CostOfGoods)),
            Row(Loc.T("Reports.GrossProfit"), M(r.GrossProfit)),
            Row(Loc.T("Reports.Margin"), $"{r.MarginPercent:0.0}%"),
            Row(Loc.T("Reports.AverageBasket"), M(r.AverageBasket)),
            new('-', 42),
            Loc.T("Common.Payments"),
        };
        lines.AddRange(r.ByPaymentMethod.Select(p => Row("  " + p.Name, M(p.Amount))));
        lines.Add(new string('-', 42));
        lines.Add(Loc.T("Reports.TopCategories"));
        lines.AddRange(r.ByCategory.Take(10).Select(c => Row($"  {c.Name} ({c.Quantity})", M(c.Amount))));
        lines.Add(new string('-', 42));
        lines.Add(Loc.T("Reports.TopProducts"));
        lines.AddRange(r.TopProducts.Take(10).Select(p => Row($"  {(p.Name.Length > 20 ? p.Name[..20] : p.Name)} ({p.Quantity})", M(p.Amount))));
        Dialogs.ShowDialog(new TextPreviewViewModel(Dialogs, print, Loc.T("Reports.SalesSummary"), lines));
    }
}
