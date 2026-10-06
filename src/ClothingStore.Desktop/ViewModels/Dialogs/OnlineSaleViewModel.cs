using System.Globalization;
using ClothingStore.Core;
using ClothingStore.Core.Localization;
using ClothingStore.Desktop.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClothingStore.Desktop.ViewModels.Dialogs;

/// <summary>Marks the sale on the register as an online order: where it came from, delivery fee, address/notes.</summary>
public sealed partial class OnlineSaleViewModel : DialogViewModelBase
{
    public OnlineSaleViewModel(IDialogService dialogs, SalesChannel channel, decimal deliveryFee, string? notes) : base(dialogs)
    {
        Channel = channel == SalesChannel.InStore ? SalesChannel.WhatsApp : channel;
        var fee = channel == SalesChannel.InStore ? LocalPreferences.Current.LastDeliveryFee ?? 0 : deliveryFee;
        DeliveryFeeText = fee == 0 ? "" : fee.ToString("0.##", CultureInfo.InvariantCulture);
        Notes = notes;
    }

    public override string Title => Loc.T("OnlineSale.Title");

    public IReadOnlyList<SalesChannel> Channels { get; } = Enum.GetValues<SalesChannel>().Where(c => c != SalesChannel.InStore).ToList();

    [ObservableProperty]
    public partial SalesChannel Channel { get; set; }

    [ObservableProperty]
    public partial string DeliveryFeeText { get; set; }

    [ObservableProperty]
    public partial string? Notes { get; set; }

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
        LocalPreferences.Current.LastDeliveryFee = DeliveryFee;
        LocalPreferences.Current.Save();
        Close(true);
    }

    [RelayCommand]
    private void Cancel() => Close(false);
}
