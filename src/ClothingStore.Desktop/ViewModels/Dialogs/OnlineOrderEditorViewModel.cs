using System.Collections.ObjectModel;
using System.Globalization;
using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Localization;
using ClothingStore.Core.Pricing;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Converters;
using ClothingStore.Desktop.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClothingStore.Desktop.ViewModels.Dialogs;

public sealed partial class OrderLineViewModel(ProductVariant variant, int quantity) : ObservableObject
{
    public ProductVariant Variant { get; } = variant;
    public string Name => Variant.Product?.Name ?? Variant.DisplayName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotal), nameof(IsShort))]
    public partial int Quantity { get; set; } = quantity;

    public decimal LineTotal => Variant.EffectivePrice * Quantity;

    /// <summary>More ordered than the system thinks is in stock.</summary>
    public bool IsShort => Quantity > Variant.StockQuantity;

    partial void OnQuantityChanged(int value)
    {
        if (value < 1) Quantity = 1;
    }
}

/// <summary>Take (or edit, while still new) an order from a chat: who, where, what, delivery fee.</summary>
public sealed partial class OnlineOrderEditorViewModel : DialogViewModelBase
{
    private readonly OnlineOrderService _orders;
    private readonly ProductService _products;
    private readonly CustomerService _customers;
    private readonly SettingsService _settings;
    private readonly Session _session;
    private readonly OnlineOrder? _existing;

    public OnlineOrderEditorViewModel(
        IDialogService dialogs, OnlineOrderService orders, ProductService products, CustomerService customers,
        SettingsService settings, Session session, OnlineOrder? existing)
        : base(dialogs)
    {
        _orders = orders;
        _products = products;
        _customers = customers;
        _settings = settings;
        _session = session;
        _existing = existing;
        Lines.CollectionChanged += (_, _) => OnLinesChanged();

        if (existing is not null)
        {
            Channel = existing.Channel;
            CustomerId = existing.CustomerId;
            CustomerName = existing.CustomerName;
            Phone = existing.Phone;
            Handle = existing.Handle;
            Address = existing.Address;
            Notes = existing.Notes;
            DeliveryFeeText = existing.DeliveryFee.ToString("0.##", CultureInfo.InvariantCulture);
            SaveCustomer = false;
        }
        else
        {
            DeliveryFeeText = LocalPreferences.Current.LastDeliveryFee?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
        }
    }

    public override string Title => _existing is null ? Loc.T("Orders.NewTitle") : Loc.T("Orders.EditTitle", _existing.OrderNumber);

    public IReadOnlyList<SalesChannel> Channels { get; } = Enum.GetValues<SalesChannel>().Where(c => c != SalesChannel.InStore).ToList();

    [ObservableProperty]
    public partial SalesChannel Channel { get; set; } = SalesChannel.WhatsApp;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomer))]
    public partial int? CustomerId { get; set; }

    public bool HasCustomer => CustomerId is not null;

    [ObservableProperty]
    public partial string CustomerName { get; set; } = "";

    [ObservableProperty]
    public partial string? Phone { get; set; }

    [ObservableProperty]
    public partial string? Handle { get; set; }

    [ObservableProperty]
    public partial string? Address { get; set; }

    [ObservableProperty]
    public partial string? Notes { get; set; }

    [ObservableProperty]
    public partial bool SaveCustomer { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeliveryFee), nameof(Total), nameof(TotalLbp))]
    public partial string DeliveryFeeText { get; set; } = "";

    public ObservableCollection<OrderLineViewModel> Lines { get; } = [];

    [ObservableProperty]
    public partial string AddText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResults))]
    public partial List<ProductVariant> Results { get; set; } = [];

    public bool HasResults => Results.Count > 0;

    public decimal ItemsTotal => Lines.Sum(l => l.LineTotal);
    public decimal DeliveryFee => TryParse(DeliveryFeeText, out var fee) && fee > 0 ? Money.Round(fee) : 0;

    /// <summary>Shelf prices + delivery; the saved order applies tax rules exactly like the register.</summary>
    public decimal Total => ItemsTotal + DeliveryFee;
    public bool ShowLbp => _settings.Current.ActiveLbpRate > 0;
    public decimal TotalLbp => Lbp.ToPay(Total, _settings.Current.ActiveLbpRate, _settings.Current.LbpRounding);
    public bool HasLines => Lines.Count > 0;

    public OnlineOrder? Saved { get; private set; }

    public override async Task OnOpenedAsync()
    {
        if (_existing is null) return;
        try
        {
            var variants = await _products.GetVariantsAsync(_existing.Lines.Select(l => l.ProductVariantId));
            foreach (var line in _existing.Lines)
                if (variants.FirstOrDefault(v => v.Id == line.ProductVariantId) is { } v)
                    AddLine(v, line.Quantity);
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Common.SomethingWentWrong"), ex);
        }
    }

    // ---- Items ------------------------------------------------------------------------------

    [RelayCommand]
    private async Task FindAsync()
    {
        var text = AddText.Trim();
        if (text.Length == 0) return;
        List<ProductVariant> found = [];
        if (!await RunAsync(async () =>
            {
                var exact = await _products.FindByCodeAsync(text);
                found = exact is not null ? [exact] : await _products.SearchVariantsAsync(text, 40);
            }))
            return;

        if (found.Count == 1)
        {
            AddLine(found[0], 1);
            AddText = "";
            Results = [];
        }
        else if (found.Count == 0)
        {
            Results = [];
            Dialogs.Warning(Loc.T("Labels.NothingFound", text));
        }
        else
        {
            Results = found;
        }
    }

    [RelayCommand]
    private void AddResult(ProductVariant variant)
    {
        AddLine(variant, 1);
        Results = [];
        AddText = "";
    }

    [RelayCommand]
    private void RemoveLine(OrderLineViewModel line) => Lines.Remove(line);

    private void AddLine(ProductVariant variant, int quantity)
    {
        if (Lines.FirstOrDefault(l => l.Variant.Id == variant.Id) is { } existing)
        {
            existing.Quantity += quantity;
            return;
        }
        var line = new OrderLineViewModel(variant, quantity);
        line.PropertyChanged += (_, _) => OnLinesChanged();
        Lines.Add(line);
    }

    private void OnLinesChanged()
    {
        OnPropertyChanged(nameof(ItemsTotal));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalLbp));
        OnPropertyChanged(nameof(HasLines));
    }

    // ---- Customer ---------------------------------------------------------------------------

    [RelayCommand]
    private void PickCustomer()
    {
        var picker = new CustomerPickerViewModel(Dialogs, _customers);
        if (!Dialogs.ShowDialog(picker) || picker.Selected is not { } c) return;
        CustomerId = c.Id;
        CustomerName = c.FullName;
        Phone = c.Phone;
        SaveCustomer = false;
    }

    [RelayCommand]
    private void ClearCustomer() => CustomerId = null;

    // ---- Save -------------------------------------------------------------------------------

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!string.IsNullOrWhiteSpace(DeliveryFeeText) && (!TryParse(DeliveryFeeText, out var fee) || fee < 0))
        {
            Dialogs.Warning(Loc.T("Orders.BadDeliveryFee"));
            return;
        }
        var input = new OnlineOrderInput
        {
            Channel = Channel,
            CustomerId = CustomerId,
            CustomerName = CustomerName,
            Phone = Phone,
            Handle = Handle,
            Address = Address,
            Notes = Notes,
            DeliveryFee = DeliveryFee,
            SaveCustomer = SaveCustomer && CustomerId is null,
            Lines = Lines.Select(l => new OnlineOrderLineInput(l.Variant.Id, l.Quantity)).ToList(),
        };

        OnlineOrder? saved = null;
        if (!await RunAsync(async () => saved = _existing is null
                ? await _orders.CreateAsync(input, _session.User.Id)
                : await _orders.UpdateAsync(_existing.Id, input)))
            return;

        LocalPreferences.Current.LastDeliveryFee = DeliveryFee;
        LocalPreferences.Current.Save();
        Saved = saved;
        Close(true);
    }

    [RelayCommand]
    private void Cancel() => Close(false);

    private static bool TryParse(string? text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value) ||
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}
