using System.Collections.ObjectModel;
using ClothingStore.Core;
using ClothingStore.Core.Localization;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Converters;
using ClothingStore.Desktop.Infrastructure;
using ClothingStore.Desktop.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClothingStore.Desktop.ViewModels;

public enum DeliveryFilter
{
    Owed,
    Paid,
    All,
}

/// <summary>
/// Online orders paid through a delivery company: what each company still owes, and recording its payments
/// (cash into the drawer, or a transfer) for the orders it covers.
/// </summary>
public sealed partial class DeliveriesViewModel(
    IDialogService dialogs, DeliveryService deliveries, SettingsService settings, Session session) : ViewModelBase(dialogs), IPageViewModel
{
    private readonly LatestSearch _search = new();

    public string Title => Loc.T("Nav.Deliveries");

    [ObservableProperty]
    public partial DeliveryFilter Filter { get; set; } = DeliveryFilter.Owed;

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    /// <summary>Company filter; null = every company.</summary>
    [ObservableProperty]
    public partial string? Courier { get; set; }

    [ObservableProperty]
    public partial List<string> Couriers { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRows))]
    public partial List<DeliveryRow> Rows { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalOwed), nameof(HasBalances))]
    public partial List<CourierBalance> Balances { get; set; } = [];

    /// <summary>Rows ticked in the list (Ctrl/Shift+click).</summary>
    public ObservableCollection<object> Selection { get; } = [];

    public bool HasRows => Rows.Count > 0;
    public bool HasBalances => Balances.Count > 0;
    public decimal TotalOwed => Balances.Sum(b => b.Owed);
    public string SelectionText
    {
        get
        {
            var owed = SelectedOwed();
            return owed.Count == 0 ? Loc.T("Deliveries.SelectHint") : Loc.T("Deliveries.Selected", owed.Count, CurrencyFormat.Format(owed.Sum(r => r.Owed)));
        }
    }

    private bool _hooked;

    public async Task OnNavigatedToAsync()
    {
        if (!_hooked)
        {
            _hooked = true;
            Selection.CollectionChanged += (_, _) =>
        {
                OnPropertyChanged(nameof(SelectionText));
                ReceivePaymentCommand.NotifyCanExecuteChanged();
            };
        }
        await RefreshAsync();
    }

    partial void OnFilterChanged(DeliveryFilter value) => _ = RefreshAsync();
    partial void OnCourierChanged(string? value) => _ = RefreshAsync();
    partial void OnSearchTextChanged(string value) => _ = RefreshAsync(immediately: false);

    [RelayCommand]
    private async Task RefreshAsync(bool immediately = true)
    {
        bool? paid = Filter switch { DeliveryFilter.Owed => false, DeliveryFilter.Paid => true, _ => null };
        var courier = string.IsNullOrWhiteSpace(Courier) ? null : Courier;
        var text = SearchText;
        try
        {
            Func<CancellationToken, Task<(List<DeliveryRow>, List<CourierBalance>, List<string>)>> load = async ct =>
                (await deliveries.GetAsync(paid, courier, text, ct: ct), await deliveries.GetBalancesAsync(ct), await deliveries.GetCouriersAsync(ct));
            Action<(List<DeliveryRow> Rows, List<CourierBalance> Balances, List<string> Couriers)> apply = r =>
            {
                Rows = r.Rows;
                Balances = r.Balances;
                Couriers = r.Couriers;
            };
            await (immediately ? _search.RunNowAsync(load, apply) : _search.RunAsync(load, apply));
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Common.SomethingWentWrong"), ex);
        }
    }

    [RelayCommand]
    private void ShowCourier(CourierBalance balance)
    {
        Filter = DeliveryFilter.Owed;
        Courier = balance.Courier.Length == 0 ? null : balance.Courier;
    }

    [RelayCommand]
    private void ClearCourier() => Courier = null;

    private List<DeliveryRow> SelectedOwed() => Selection.OfType<DeliveryRow>().Where(r => !r.IsPaid).ToList();
    private bool CanReceive() => SelectedOwed().Count > 0;

    /// <summary>The delivery company paid for the selected orders.</summary>
    [RelayCommand(CanExecute = nameof(CanReceive))]
    private async Task ReceivePaymentAsync()
    {
        var rows = SelectedOwed();
        if (rows.Count == 0) return;
        var dialog = new SettleDeliveriesViewModel(Dialogs, settings.Current, rows, session.HasOpenShift);
        if (!Dialogs.ShowDialog(dialog)) return;

        var ok = await RunAsync(() => deliveries.SettleAsync(new SettleDeliveriesRequest
        {
            SaleIds = rows.Select(r => r.SaleId).ToList(),
            UserId = session.User.Id,
            ShiftId = session.CurrentShift?.Id,
            Method = dialog.Method,
            Received = dialog.Received,
            ExchangeRate = dialog.Rate,
            Reference = dialog.Reference,
        }));
        if (!ok)
        {
            try { await settings.RefreshCurrencyAsync(); } catch { /* checked again next time */ }
            return;
        }
        Dialogs.Toast(Loc.T("Deliveries.Recorded", rows.Count));
        await RefreshAsync();
    }
}
