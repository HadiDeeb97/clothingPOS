using System.Diagnostics;
using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Localization;
using ClothingStore.Core.Pricing;
using ClothingStore.Core.Receipts;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Converters;
using ClothingStore.Desktop.Infrastructure;
using ClothingStore.Desktop.Services;
using ClothingStore.Desktop.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClothingStore.Desktop.ViewModels;

/// <summary>A status tab above the order list ("Open", "New", ... "All").</summary>
public sealed partial class OrderFilter(string key, OnlineOrderStatus? status, bool openOnly) : ObservableObject
{
    public string Title { get; } = Loc.T(key);
    public OnlineOrderStatus? Status { get; } = status;
    public bool OpenOnly { get; } = openOnly;

    [ObservableProperty]
    public partial int Count { get; set; }
}

/// <summary>
/// Orders from WhatsApp, Instagram, Facebook and the phone: take them, confirm (stock is held), send out for delivery,
/// and complete when the money is collected (a normal sale, with the delivery fee). Cancelling puts stock back.
/// </summary>
public sealed partial class OnlineOrdersViewModel(
    IDialogService dialogs, OnlineOrderService orders, ProductService products, CustomerService customers,
    SettingsService settings, Session session, PrintService print) : ViewModelBase(dialogs), IPageViewModel
{
    private readonly LatestSearch _search = new();

    public string Title => Loc.T("Nav.OnlineOrders");

    public IReadOnlyList<OrderFilter> Filters { get; } =
    [
        new("Orders.Filter.Open", null, true),
        new("Enum.OnlineOrderStatus.New", OnlineOrderStatus.New, false),
        new("Enum.OnlineOrderStatus.Confirmed", OnlineOrderStatus.Confirmed, false),
        new("Enum.OnlineOrderStatus.OutForDelivery", OnlineOrderStatus.OutForDelivery, false),
        new("Enum.OnlineOrderStatus.Completed", OnlineOrderStatus.Completed, false),
        new("Enum.OnlineOrderStatus.Cancelled", OnlineOrderStatus.Cancelled, false),
        new("Orders.Filter.All", null, false),
    ];

    [ObservableProperty]
    public partial OrderFilter? SelectedFilter { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial List<OnlineOrder> Orders { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(ConfirmCommand), nameof(OutForDeliveryCommand), nameof(CompleteCommand),
        nameof(CancelOrderCommand), nameof(DeliveryNoteCommand), nameof(WhatsAppCommand), nameof(CopySummaryCommand))]
    public partial OnlineOrder? SelectedOrder { get; set; }

    /// <summary>Full details (lines, customer, sale) of <see cref="SelectedOrder"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail), nameof(DetailLbp), nameof(ShowLbp), nameof(StatusHint))]
    public partial OnlineOrder? Detail { get; set; }

    public bool HasDetail => Detail is not null;
    public bool ShowLbp => settings.Current.ActiveLbpRate > 0;
    public decimal DetailLbp => Detail is null ? 0 : Lbp.ToPay(Detail.Total, settings.Current.ActiveLbpRate, settings.Current.LbpRounding);

    /// <summary>What happens next, under the status chip.</summary>
    public string StatusHint => Detail?.Status switch
    {
        OnlineOrderStatus.New => Loc.T("Orders.Hint.New"),
        OnlineOrderStatus.Confirmed => Loc.T("Orders.Hint.Confirmed"),
        OnlineOrderStatus.OutForDelivery => Loc.T("Orders.Hint.OutForDelivery", Detail.Courier ?? "-"),
        OnlineOrderStatus.Completed => Loc.T("Orders.Hint.Completed", Detail.Sale?.ReceiptNumber ?? "-"),
        OnlineOrderStatus.Cancelled => Loc.T("Orders.Hint.Cancelled", Detail.CancelReason ?? "-"),
        _ => "",
    };

    public async Task OnNavigatedToAsync()
    {
        SelectedFilter ??= Filters[0];
        await RefreshAsync();
    }

    partial void OnSelectedFilterChanged(OrderFilter? value) => _ = RefreshAsync();
    partial void OnSearchTextChanged(string value) => _ = RefreshAsync(immediately: false);

    async partial void OnSelectedOrderChanged(OnlineOrder? value)
    {
        try
        {
            Detail = value is null ? null : await orders.GetAsync(value.Id);
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Common.SomethingWentWrong"), ex);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync(bool immediately = true)
    {
        var filter = SelectedFilter ?? Filters[0];
        var text = SearchText;
        var selectedId = SelectedOrder?.Id;
        try
        {
            Func<CancellationToken, Task<(List<OnlineOrder> List, OnlineOrderCounts Counts)>> load = async ct =>
                (await orders.SearchAsync(filter.Status, text, filter.OpenOnly, ct: ct), await orders.GetCountsAsync(ct));
            Action<(List<OnlineOrder> List, OnlineOrderCounts Counts)> apply = result =>
            {
                Orders = result.List;
                Filters[0].Count = result.Counts.Open;
                Filters[1].Count = result.Counts.New;
                Filters[2].Count = result.Counts.Confirmed;
                Filters[3].Count = result.Counts.OutForDelivery;
                SelectedOrder = Orders.FirstOrDefault(o => o.Id == selectedId) ?? Orders.FirstOrDefault();
            };
            await (immediately ? _search.RunNowAsync(load, apply) : _search.RunAsync(load, apply));
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Common.SomethingWentWrong"), ex);
        }
    }

    private async Task ReloadDetailAsync()
    {
        var id = SelectedOrder?.Id;
        await RefreshAsync();
        if (id is not null && SelectedOrder?.Id == id) Detail = await orders.GetAsync(id.Value);
    }

    // ---- Actions ----------------------------------------------------------------------------

    [RelayCommand]
    private async Task NewAsync()
    {
        var editor = new OnlineOrderEditorViewModel(Dialogs, orders, products, customers, settings, session, null);
        if (!Dialogs.ShowDialog(editor) || editor.Saved is null) return;
        Dialogs.Toast(Loc.T("Orders.Created", editor.Saved.OrderNumber));
        SearchText = "";
        if (SelectedFilter is { OpenOnly: false, Status: not (null or OnlineOrderStatus.New) }) SelectedFilter = Filters[0];
        await RefreshAsync();
        SelectedOrder = Orders.FirstOrDefault(o => o.Id == editor.Saved.Id) ?? SelectedOrder;
    }

    private bool IsStatus(params OnlineOrderStatus[] statuses) => Detail is not null && statuses.Contains(Detail.Status);
    private bool CanEdit() => IsStatus(OnlineOrderStatus.New);
    private bool CanConfirm() => IsStatus(OnlineOrderStatus.New);
    private bool CanSend() => IsStatus(OnlineOrderStatus.New, OnlineOrderStatus.Confirmed);
    private bool CanComplete() => IsStatus(OnlineOrderStatus.New, OnlineOrderStatus.Confirmed, OnlineOrderStatus.OutForDelivery);
    private bool HasOrder() => Detail is not null;

    partial void OnDetailChanged(OnlineOrder? value)
    {
        EditCommand.NotifyCanExecuteChanged();
        ConfirmCommand.NotifyCanExecuteChanged();
        OutForDeliveryCommand.NotifyCanExecuteChanged();
        CompleteCommand.NotifyCanExecuteChanged();
        CancelOrderCommand.NotifyCanExecuteChanged();
        DeliveryNoteCommand.NotifyCanExecuteChanged();
        WhatsAppCommand.NotifyCanExecuteChanged();
        CopySummaryCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task EditAsync()
    {
        if (Detail is null) return;
        var editor = new OnlineOrderEditorViewModel(Dialogs, orders, products, customers, settings, session, Detail);
        if (Dialogs.ShowDialog(editor)) await ReloadDetailAsync();
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        if (Detail is not { } order) return;
        if (await RunAsync(() => orders.ConfirmAsync(order.Id, session.User.Id)))
        {
            Dialogs.Toast(Loc.T("Orders.ConfirmedToast", order.OrderNumber));
            await ReloadDetailAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task OutForDeliveryAsync()
    {
        if (Detail is not { } order) return;
        var courier = Dialogs.Prompt(Loc.T("Orders.SendTitle"), Loc.T("Orders.CourierPrompt", order.OrderNumber), order.Courier ?? "");
        if (courier is null) return;
        if (await RunAsync(() => orders.MarkOutForDeliveryAsync(order.Id, courier, session.User.Id)))
        {
            Dialogs.Toast(Loc.T("Orders.SentToast", order.OrderNumber));
            await ReloadDetailAsync();
        }
    }

    /// <summary>The driver brought the money (or the customer paid by wallet/card): record the sale.</summary>
    [RelayCommand(CanExecute = nameof(CanComplete))]
    private async Task CompleteAsync()
    {
        if (Detail is not { } order) return;
        var payment = new PaymentViewModel(Dialogs, order.Total, order.Customer, settings.Current);
        if (!Dialogs.ShowDialog(payment)) return;

        Sale? sale = null;
        var ok = await RunAsync(async () => sale = await orders.CompleteAsync(order.Id, new CompleteOnlineOrderRequest
        {
            UserId = session.User.Id,
            ShiftId = session.CurrentShift?.Id,
            Payments = payment.Payments,
            ChangeIn = payment.EffectiveChangeIn,
            ExchangeRate = payment.Rate,
        }));
        if (!ok || sale is null)
        {
            try { await settings.RefreshCurrencyAsync(); } catch { /* checked again on the next try */ }
            return;
        }

        await ReloadDetailAsync();
        var receipt = ReceiptFormatter.Format(ReceiptBuilder.FromSale(sale, settings.Current), settings.Current.ReceiptWidth);
        Dialogs.ShowDialog(new TextPreviewViewModel(Dialogs, print, Loc.T("Orders.CompletedTitle", order.OrderNumber, sale.ReceiptNumber), receipt));
    }

    [RelayCommand(CanExecute = nameof(CanComplete))]
    private async Task CancelOrderAsync()
    {
        if (Detail is not { } order) return;
        var reason = Dialogs.Prompt(Loc.T("Orders.CancelTitle"), Loc.T("Orders.CancelPrompt", order.OrderNumber));
        if (reason is null) return;
        if (await RunAsync(() => orders.CancelAsync(order.Id, reason, session.User.Id)))
        {
            Dialogs.Toast(Loc.T("Orders.CancelledToast", order.OrderNumber), ToastKind.Info);
            await ReloadDetailAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(HasOrder))]
    private void DeliveryNote()
    {
        if (Detail is null) return;
        Dialogs.ShowDialog(new TextPreviewViewModel(Dialogs, print, Loc.T("Orders.DeliveryNote"), ReceiptBuilder.DeliveryNote(Detail, settings.Current)));
    }

    /// <summary>Opens WhatsApp (app or web) with a message to the customer about this order, ready to send.</summary>
    [RelayCommand(CanExecute = nameof(HasOrder))]
    private void WhatsApp()
    {
        if (Detail is null) return;
        var link = PhoneLinks.WhatsAppLink(Detail.Phone, CustomerMessage(Detail));
        if (link is null)
        {
            Dialogs.Warning(Loc.T("Orders.NoPhone"));
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Orders.WhatsAppFailed"), ex);
        }
    }

    /// <summary>The same message on the clipboard, to paste into Instagram or Facebook.</summary>
    [RelayCommand(CanExecute = nameof(HasOrder))]
    private void CopySummary()
    {
        if (Detail is null) return;
        try
        {
            System.Windows.Clipboard.SetText(CustomerMessage(Detail));
            Dialogs.Toast(Loc.T("Orders.Copied"));
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Common.SomethingWentWrong"), ex);
        }
    }

    /// <summary>Order summary for the customer, in the receipt language.</summary>
    private string CustomerMessage(OnlineOrder o)
    {
        var s = settings.Current;
        var lang = s.ReceiptLanguage;
        string R(string key, params object?[] args) => Loc.Format(lang, key, args);
        string M(decimal v) => Money.Format(v, s.CurrencySymbol);

        var lines = new List<string> { R("Orders.Msg.Greeting", o.CustomerName, s.StoreName), R("Orders.Msg.Order", o.OrderNumber) };
        lines.AddRange(o.Lines.Select(l => R("Orders.Msg.Line", l.Quantity, l.ProductName,
            string.IsNullOrWhiteSpace(l.VariantDescription) ? "" : $" ({l.VariantDescription})", M(l.LineTotal))));
        if (o.DeliveryFee > 0) lines.Add(R("Orders.Msg.Delivery", M(o.DeliveryFee)));
        var lbp = s.ActiveLbpRate > 0 ? $" ({Lbp.Format(Lbp.ToPay(o.Total, s.ActiveLbpRate, s.LbpRounding), lang)})" : "";
        lines.Add(R("Orders.Msg.Total", M(o.Total) + lbp));
        if (!string.IsNullOrWhiteSpace(o.Address)) lines.Add(R("Orders.Msg.Address", o.Address));
        lines.Add(o.Status switch
        {
            OnlineOrderStatus.New => R("Orders.Msg.PleaseConfirm"),
            OnlineOrderStatus.Confirmed => R("Orders.Msg.Confirmed"),
            OnlineOrderStatus.OutForDelivery => R("Orders.Msg.OnTheWay"),
            OnlineOrderStatus.Completed => R("Orders.Msg.Thanks"),
            _ => R("Orders.Msg.Cancelled"),
        });
        return string.Join("\n", lines);
    }
}
