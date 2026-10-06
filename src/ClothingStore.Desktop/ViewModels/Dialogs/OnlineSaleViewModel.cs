using System.Globalization;
using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Localization;
using ClothingStore.Desktop.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClothingStore.Desktop.ViewModels.Dialogs;

/// <summary>Marks the sale on the register as an online order: where it came from, delivery fee, address/notes.</summary>
public sealed partial class OnlineSaleViewModel : DialogViewModelBase
{
    public OnlineSaleViewModel(
        IDialogService dialogs, SalesChannel channel, decimal deliveryFee, string? notes, string? courier, IReadOnlyList<string> couriers,
        string? deliveryReference = null, IReadOnlyList<DeliveryPartner>? partners = null)
        : base(dialogs)
    {
        _partners = partners ?? [];
        DeliveryReference = deliveryReference;
        Couriers = couriers;
        var isNew = channel == SalesChannel.InStore;
        Courier = courier ?? (isNew ? LocalPreferences.Current.LastCourier ?? couriers.FirstOrDefault() : null);
        Channel = isNew ? SalesChannel.WhatsApp : channel;
        var fee = isNew ? Partner(Courier)?.DefaultFee ?? LocalPreferences.Current.LastDeliveryFee ?? 0 : deliveryFee;
        DeliveryFeeText = fee == 0 ? "" : fee.ToString("0.##", CultureInfo.CurrentCulture);
        Notes = notes;
        _ready = true;
    }

    private readonly IReadOnlyList<DeliveryPartner> _partners;
    private readonly bool _ready;

    private DeliveryPartner? Partner(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : _partners.FirstOrDefault(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Phone and contact of the chosen company / driver, when saved under Deliveries → Companies &amp; drivers.</summary>
    public string? CourierInfo => Partner(Courier) is { } p
        ? string.Join("  ·  ", new[] { Loc.T($"Enum.DeliveryPartnerKind.{p.Kind}"), p.ContactName, p.Phone, p.Phone2 }.Where(x => !string.IsNullOrWhiteSpace(x)))
        : null;

    partial void OnCourierChanged(string? value)
    {
        OnPropertyChanged(nameof(CourierInfo));
        // Picking a partner fills in its usual fee (not while the window is opening with the order's own fee).
        if (_ready && Partner(value)?.DefaultFee is { } fee)
            DeliveryFeeText = fee == 0 ? "" : fee.ToString("0.##", CultureInfo.CurrentCulture);
    }

    public override string Title => Loc.T("OnlineSale.Title");

    public IReadOnlyList<SalesChannel> Channels { get; } = Enum.GetValues<SalesChannel>().Where(c => c != SalesChannel.InStore).ToList();

    [ObservableProperty]
    public partial SalesChannel Channel { get; set; }

    [ObservableProperty]
    public partial string DeliveryFeeText { get; set; }

    [ObservableProperty]
    public partial string? Notes { get; set; }

    /// <summary>Delivery company or driver: saved partners and companies used before are offered.</summary>
    [ObservableProperty]
    public partial string? Courier { get; set; }

    public IReadOnlyList<string> Couriers { get; }

    /// <summary>The delivery company's invoice / tracking number: type it or scan the barcode on its slip.</summary>
    [ObservableProperty]
    public partial string? DeliveryReference { get; set; }

    public decimal DeliveryFee { get; private set; }

    [RelayCommand]
    private void PickChannel(SalesChannel channel) => Channel = channel;

    [RelayCommand]
    private void Save()
    {
        decimal fee = 0;
        if (!string.IsNullOrWhiteSpace(DeliveryFeeText) &&
            !(decimal.TryParse(DeliveryFeeText, NumberStyles.Number, CultureInfo.CurrentCulture, out fee) ||
              decimal.TryParse(DeliveryFeeText, NumberStyles.Number, CultureInfo.InvariantCulture, out fee)) || fee < 0)
        {
            Dialogs.Warning(Loc.T("OnlineSale.BadFee"));
            return;
        }
        DeliveryFee = Money.Round(fee);
        Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
        Courier = string.IsNullOrWhiteSpace(Courier) ? null : Courier.Trim();
        DeliveryReference = string.IsNullOrWhiteSpace(DeliveryReference) ? null : DeliveryReference.Trim().ToUpperInvariant();
        LocalPreferences.Current.LastDeliveryFee = DeliveryFee;
        LocalPreferences.Current.LastCourier = Courier;
        LocalPreferences.Current.Save();
        Close(true);
    }

    [RelayCommand]
    private void Cancel() => Close(false);
}
