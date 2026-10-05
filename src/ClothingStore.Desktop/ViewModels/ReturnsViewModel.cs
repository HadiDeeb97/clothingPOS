using System.Collections.ObjectModel;
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

public sealed partial class ReturnLineViewModel(SaleLine line) : ObservableObject
{
    public SaleLine Line { get; } = line;
    public decimal UnitRefund => Line.Quantity == 0 ? 0 : Money.Round(Line.LineTotal / Line.Quantity);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstimatedRefund))]
    public partial int ReturnQuantity { get; set; }

    [ObservableProperty]
    public partial bool Restock { get; set; } = true;

    public decimal EstimatedRefund => UnitRefund * ReturnQuantity;

    partial void OnReturnQuantityChanged(int value)
    {
        if (value < 0) ReturnQuantity = 0;
        else if (value > Line.ReturnableQuantity) ReturnQuantity = Line.ReturnableQuantity;
    }
}

/// <summary>Refunds or exchanges: look up a receipt, choose items, refund to cash/card/store credit.</summary>
public sealed partial class ReturnsViewModel(
    IDialogService dialogs, SalesService sales, ReturnService returns, SettingsService settings,
    UserService users, Session session, PrintService print) : ViewModelBase(dialogs), IPageViewModel
{
    public string Title => "Returns & Exchanges";

    public string[] ReasonPresets { get; } =
        ["Wrong size", "Changed mind", "Defective / damaged", "Not as described", "Unwanted gift", "Exchange"];

    [ObservableProperty]
    public partial string ReceiptNumber { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSale), nameof(CanRefundToCredit), nameof(IsOutsideWindow), nameof(WindowMessage))]
    public partial Sale? Sale { get; set; }

    public ObservableCollection<ReturnLineViewModel> Lines { get; } = [];

    [ObservableProperty]
    public partial RefundMethod RefundMethod { get; set; } = RefundMethod.Cash;

    [ObservableProperty]
    public partial string? Reason { get; set; }

    [ObservableProperty]
    public partial decimal EstimatedRefund { get; private set; }

    public bool HasSale => Sale is not null;
    public bool CanRefundToCredit => Sale?.CustomerId is not null;

    public bool IsOutsideWindow =>
        Sale is not null && settings.Current.ReturnWindowDays > 0 &&
        (DateTime.Now - Sale.CreatedAt).TotalDays > settings.Current.ReturnWindowDays;

    public string WindowMessage => IsOutsideWindow
        ? $"This sale is older than the {settings.Current.ReturnWindowDays}-day return window — manager approval is required."
        : "";

    public Task OnNavigatedToAsync() => Task.CompletedTask;

    public async Task LoadReceiptAsync(string receiptNumber)
    {
        ReceiptNumber = receiptNumber;
        await FindAsync();
    }

    [RelayCommand]
    private Task FindAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(ReceiptNumber)) return;
        var sale = await sales.GetByReceiptAsync(ReceiptNumber);
        if (sale is null)
        {
            Clear();
            Dialogs.Warning($"Receipt {ReceiptNumber.Trim().ToUpperInvariant()} was not found.");
            return;
        }
        if (sale.Status == SaleStatus.Voided)
        {
            Clear();
            Dialogs.Warning("That sale was voided and cannot be returned.");
            return;
        }
        Show(sale);
    });

    private void Show(Sale sale)
    {
        foreach (var l in Lines) l.PropertyChanged -= OnLineChanged;
        Lines.Clear();
        Sale = sale;
        foreach (var line in sale.Lines)
        {
            var vm = new ReturnLineViewModel(line);
            vm.PropertyChanged += OnLineChanged;
            Lines.Add(vm);
        }
        RefundMethod = sale.Payments.Any(p => p.Method == PaymentMethod.Card) ? RefundMethod.Card : RefundMethod.Cash;
        Reason = null;
        UpdateTotal();
    }

    private void OnLineChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => UpdateTotal();

    private void UpdateTotal() => EstimatedRefund = Lines.Sum(l => l.EstimatedRefund);

    [RelayCommand]
    private void ReturnAll()
    {
        foreach (var line in Lines) line.ReturnQuantity = line.Line.ReturnableQuantity;
    }

    [RelayCommand]
    private void Clear()
    {
        foreach (var l in Lines) l.PropertyChanged -= OnLineChanged;
        Lines.Clear();
        Sale = null;
        Reason = null;
        EstimatedRefund = 0;
    }

    [RelayCommand]
    private async Task ProcessAsync()
    {
        if (Sale is not { } sale) return;
        var selected = Lines.Where(l => l.ReturnQuantity > 0).ToList();
        if (selected.Count == 0)
        {
            Dialogs.Warning("Set the quantity to return for at least one item.");
            return;
        }
        if (RefundMethod == RefundMethod.StoreCredit && !CanRefundToCredit)
        {
            Dialogs.Warning("Store credit needs a customer on the original sale. Choose cash or card.");
            return;
        }
        if (RefundMethod == RefundMethod.Cash && !session.HasOpenShift)
        {
            Dialogs.Warning("Open a cash drawer shift before giving cash refunds.");
            return;
        }

        int? approvedBy = null;
        if (IsOutsideWindow && !session.Can(Permission.OverrideDiscountLimit))
        {
            var approval = new ManagerApprovalViewModel(Dialogs, users, WindowMessage, Permission.OverrideDiscountLimit);
            if (!Dialogs.ShowDialog(approval) || approval.ApprovedBy is null) return;
            approvedBy = approval.ApprovedBy.Id;
        }

        if (!Dialogs.Confirm($"Refund {Converters.CurrencyFormat.Format(EstimatedRefund)} to {Converters.EnumDisplayConverter.Humanize(RefundMethod.ToString())}?"))
            return;

        SaleReturn? result = null;
        var ok = await RunAsync(async () =>
        {
            result = await returns.ProcessReturnAsync(new ReturnRequest
            {
                SaleId = sale.Id,
                UserId = session.User.Id,
                ShiftId = session.CurrentShift?.Id,
                RefundMethod = RefundMethod,
                Reason = Reason,
                ApprovedByUserId = approvedBy,
                Lines = selected.Select(l => new ReturnLineRequest(l.Line.Id, l.ReturnQuantity, l.Restock)).ToList(),
            });
        });
        if (!ok || result is null) return;

        var doc = ReceiptBuilder.FromReturn(result, sale, settings.Current, session.User.FullName);
        Dialogs.ShowDialog(new TextPreviewViewModel(Dialogs, print, $"Refund {result.ReturnNumber}",
            ReceiptFormatter.Format(doc, settings.Current.ReceiptWidth)));

        if (RefundMethod == RefundMethod.StoreCredit)
            Dialogs.Info("Store credit added. For an exchange, ring up the new items on the Register and pay with store credit.");

        await FindAsync(); // refresh remaining returnable quantities
    }
}
