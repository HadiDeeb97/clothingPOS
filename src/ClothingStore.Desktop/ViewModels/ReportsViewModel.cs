using ClothingStore.Core;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Converters;
using ClothingStore.Desktop.Infrastructure;
using ClothingStore.Desktop.Services;
using ClothingStore.Desktop.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClothingStore.Desktop.ViewModels;

public sealed partial class ReportsViewModel(
    IDialogService dialogs, ReportService reports, SettingsService settings, PrintService print)
    : ViewModelBase(dialogs), IPageViewModel
{
    public string Title => "Reports";

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
        var path = Dialogs.SaveFile("Export report", "CSV files (*.csv)|*.csv", $"{section}_{From:yyyyMMdd}_{To:yyyyMMdd}.csv");
        if (path is null) return;
        try
        {
            switch (section)
            {
                case "daily":
                    CsvExporter.Write(path, ["Date", "Transactions", "Total"],
                        Report.ByDay.Select(d => new object?[] { d.Date.ToString("yyyy-MM-dd"), d.Transactions, d.Total }));
                    break;
                case "payments":
                    CsvExporter.Write(path, ["Method", "Count", "Amount"], Report.ByPaymentMethod.Select(x => new object?[] { x.Name, x.Count, x.Amount }));
                    break;
                case "cashiers":
                    CsvExporter.Write(path, ["Cashier", "Transactions", "Items", "Total"], Report.ByCashier.Select(x => new object?[] { x.Name, x.Count, x.Quantity, x.Amount }));
                    break;
                default:
                    var rows = section switch
                    {
                        "categories" => Report.ByCategory,
                        "sizes" => Report.BySize,
                        _ => Report.TopProducts,
                    };
                    CsvExporter.Write(path, ["Name", "Units", "Net sales"], rows.Select(x => new object?[] { x.Name, x.Quantity, x.Amount }));
                    break;
            }
            Dialogs.Info("Report exported.");
        }
        catch (Exception ex)
        {
            Dialogs.Error("Export failed.", ex);
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
            $"SALES SUMMARY {r.From:yyyy-MM-dd} to {r.To.AddDays(-1):yyyy-MM-dd}",
            new('-', 42),
            Row("Transactions", r.Transactions.ToString()),
            Row("Items sold", r.ItemsSold.ToString()),
            Row("Gross sales", M(r.GrossSales)),
            Row("Discounts", M(-r.Discounts)),
            Row("Tax collected", M(r.Tax)),
            Row("Total sales (incl. tax)", M(r.TotalSales)),
            Row("Refunds", M(-r.Refunds)),
            Row("Net revenue (excl. tax)", M(r.NetRevenue)),
            Row("Cost of goods", M(r.CostOfGoods)),
            Row("Gross profit", M(r.GrossProfit)),
            Row("Margin", $"{r.MarginPercent:0.0}%"),
            Row("Average basket", M(r.AverageBasket)),
            new('-', 42),
            "PAYMENTS",
        };
        lines.AddRange(r.ByPaymentMethod.Select(p => Row("  " + EnumDisplayConverter.Humanize(p.Name), M(p.Amount))));
        lines.Add(new string('-', 42));
        lines.Add("TOP CATEGORIES");
        lines.AddRange(r.ByCategory.Take(10).Select(c => Row($"  {c.Name} ({c.Quantity})", M(c.Amount))));
        lines.Add(new string('-', 42));
        lines.Add("TOP PRODUCTS");
        lines.AddRange(r.TopProducts.Take(10).Select(p => Row($"  {(p.Name.Length > 20 ? p.Name[..20] : p.Name)} ({p.Quantity})", M(p.Amount))));
        Dialogs.ShowDialog(new TextPreviewViewModel(Dialogs, print, "Sales summary", lines));
    }
}
