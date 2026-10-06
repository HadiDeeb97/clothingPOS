using System.Collections.ObjectModel;
using System.Globalization;
using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Converters;
using ClothingStore.Desktop.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClothingStore.Core.Localization;

namespace ClothingStore.Desktop.ViewModels.Dialogs;

public sealed partial class DiscountViewModel(IDialogService dialogs, string title, decimal baseAmount, DiscountType type, decimal value)
    : DialogViewModelBase(dialogs)
{
    public override string Title { get; } = title;
    public decimal BaseAmount { get; } = baseAmount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    public partial DiscountType DiscountType { get; set; } = type == DiscountType.None ? DiscountType.Percent : type;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    public partial string ValueText { get; set; } = value == 0 ? "" : value.ToString("0.##");

    public decimal Value { get; private set; }

    public string Preview => TryParse(out var v)
        ? Loc.T("Discount.Preview", CurrencyFormat.Format(Core.Pricing.CartCalculator.ResolveDiscount(BaseAmount, DiscountType, v)), CurrencyFormat.Format(BaseAmount))
        : "";

    public decimal[] QuickPercents { get; } = [5, 10, 15, 20, 25, 50];

    [RelayCommand]
    private void QuickPercent(decimal percent)
    {
        DiscountType = DiscountType.Percent;
        ValueText = percent.ToString("0.##");
    }

    [RelayCommand]
    private void Apply()
    {
        if (!TryParse(out var v) || v < 0 || (DiscountType == DiscountType.Percent && v > 100))
        {
            Dialogs.Warning(Loc.T("Discount.Invalid"));
            return;
        }
        Value = v;
        if (v == 0) DiscountType = DiscountType.None;
        Close(true);
    }

    [RelayCommand]
    private void Remove()
    {
        DiscountType = DiscountType.None;
        Value = 0;
        Close(true);
    }

    [RelayCommand]
    private void Cancel() => Close(false);

    private bool TryParse(out decimal v) =>
        decimal.TryParse(ValueText, NumberStyles.Number, CultureInfo.CurrentCulture, out v) ||
        decimal.TryParse(ValueText, NumberStyles.Number, CultureInfo.InvariantCulture, out v);
}

public sealed partial class TenderViewModel(PaymentMethod method, decimal amount, string? reference) : ObservableObject
{
    public PaymentMethod Method { get; } = method;
    public decimal Amount { get; } = amount;
    public string? Reference { get; } = reference;
}

public sealed record QuickCash(string Label, decimal Amount);

/// <summary>Tender screen supporting split payments and cash change.</summary>
public sealed partial class PaymentViewModel : DialogViewModelBase
{
    private readonly StoreSettings _settings;

    public PaymentViewModel(IDialogService dialogs, decimal total, Customer? customer, StoreSettings settings) : base(dialogs)
    {
        Total = total;
        Customer = customer;
        _settings = settings;
        Tenders.CollectionChanged += (_, _) => Refresh();
        Refresh();
    }

    public override string Title => Loc.T("Payment.Title");
    public decimal Total { get; }
    public Customer? Customer { get; }
    public ObservableCollection<TenderViewModel> Tenders { get; } = [];

    public decimal Paid => Tenders.Sum(t => t.Amount);
    public decimal Remaining => Math.Max(0, Total - Paid);
    public decimal Change => Math.Max(0, Paid - Total);
    public bool IsFullyPaid => Paid >= Total;

    public decimal StoreCreditAvailable => Customer?.StoreCredit ?? 0;
    public decimal LoyaltyValueAvailable => Customer is null ? 0 : Money.Round(Math.Floor(Customer.LoyaltyPoints * _settings.LoyaltyPointValue * 100) / 100);
    public bool CanUseStoreCredit => StoreCreditAvailable > 0;
    public bool CanUseLoyalty => LoyaltyValueAvailable > 0 && _settings.LoyaltyPointValue > 0;
    public string LoyaltyInfo => Customer is null ? "" : Loc.T("Payment.LoyaltyInfo", Customer.LoyaltyPoints, CurrencyFormat.Format(LoyaltyValueAvailable));

    [ObservableProperty]
    public partial string AmountText { get; set; } = "";

    [ObservableProperty]
    public partial string? Reference { get; set; }

    [ObservableProperty]
    public partial List<QuickCash> QuickCashOptions { get; set; } = [];

    public IReadOnlyList<PaymentInput> Payments => Tenders.Select(t => new PaymentInput(t.Method, t.Amount, t.Reference)).ToList();

    [RelayCommand]
    private void AddTender(PaymentMethod method)
    {
        if (!TryParseAmount(out var amount) || amount <= 0)
        {
            Dialogs.Warning(Loc.T("Payment.EnterAmount"));
            return;
        }

        if (method != PaymentMethod.Cash && amount > Remaining)
        {
            Dialogs.Warning(Loc.T("Payment.CantExceed", Loc.EnumText(method), CurrencyFormat.Format(Remaining)));
            return;
        }
        if (method == PaymentMethod.StoreCredit && amount > StoreCreditAvailable - Used(PaymentMethod.StoreCredit))
        {
            Dialogs.Warning(Loc.T("Payment.CreditAvailable", CurrencyFormat.Format(StoreCreditAvailable)));
            return;
        }
        if (method == PaymentMethod.LoyaltyPoints && amount > LoyaltyValueAvailable - Used(PaymentMethod.LoyaltyPoints))
        {
            Dialogs.Warning(Loc.T("Payment.PointsWorth", CurrencyFormat.Format(LoyaltyValueAvailable)));
            return;
        }
        if (Remaining == 0)
        {
            Dialogs.Warning(Loc.T("Payment.AlreadyPaid"));
            return;
        }

        Tenders.Add(new TenderViewModel(method, Money.Round(amount), method is PaymentMethod.Card or PaymentMethod.MobileWallet ? Reference : null));
        Reference = null;
    }

    [RelayCommand]
    private void UseFullStoreCredit()
    {
        AmountText = Math.Min(StoreCreditAvailable - Used(PaymentMethod.StoreCredit), Remaining).ToString("0.00");
        AddTender(PaymentMethod.StoreCredit);
    }

    [RelayCommand]
    private void UseFullLoyalty()
    {
        AmountText = Math.Min(LoyaltyValueAvailable - Used(PaymentMethod.LoyaltyPoints), Remaining).ToString("0.00");
        AddTender(PaymentMethod.LoyaltyPoints);
    }

    [RelayCommand]
    private void QuickCashTender(QuickCash option)
    {
        if (Remaining == 0) return;
        Tenders.Add(new TenderViewModel(PaymentMethod.Cash, option.Amount, null));
    }

    [RelayCommand]
    private void RemoveTender(TenderViewModel tender) => Tenders.Remove(tender);

    [RelayCommand]
    private void Complete()
    {
        if (!IsFullyPaid)
        {
            Dialogs.Warning(Loc.T("Payment.StillToPay", CurrencyFormat.Format(Remaining)));
            return;
        }
        Close(true);
    }

    [RelayCommand]
    private void Cancel() => Close(false);

    private decimal Used(PaymentMethod method) => Tenders.Where(t => t.Method == method).Sum(t => t.Amount);

    private void Refresh()
    {
        OnPropertyChanged(nameof(Paid));
        OnPropertyChanged(nameof(Remaining));
        OnPropertyChanged(nameof(Change));
        OnPropertyChanged(nameof(IsFullyPaid));
        AmountText = Remaining.ToString("0.00");

        var remaining = Remaining;
        var options = new List<QuickCash>();
        if (remaining > 0)
        {
            options.Add(new QuickCash(Loc.T("Payment.Exact"), remaining));
            foreach (var note in new[] { 5m, 10m, 20m, 50m, 100m, 200m })
            {
                var rounded = Math.Ceiling(remaining / note) * note;
                if (rounded > remaining && options.All(o => o.Amount != rounded))
                    options.Add(new QuickCash(CurrencyFormat.Format(rounded), rounded));
                if (options.Count >= 6) break;
            }
        }
        QuickCashOptions = options;
    }

    private bool TryParseAmount(out decimal amount) =>
        decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out amount) ||
        decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
}
