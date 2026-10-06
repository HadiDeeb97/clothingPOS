using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Pricing;
using ClothingStore.Core.Security;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Converters;
using ClothingStore.Desktop.Infrastructure;
using ClothingStore.Desktop.Services;
using ClothingStore.Desktop.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClothingStore.Desktop.ViewModels;

/// <summary>The register: scan or search items, build the cart, discount, hold/resume and take payment.</summary>
public sealed partial class SalesViewModel : ViewModelBase, IPageViewModel
{
    private readonly ProductService _products;
    private readonly SalesService _sales;
    private readonly CustomerService _customers;
    private readonly SettingsService _settings;
    private readonly UserService _users;
    private readonly PrintService _print;
    private readonly INavigationService _navigation;

    private int? _approvedByUserId;
    private decimal _approvedDiscountPercent;

    public SalesViewModel(
        IDialogService dialogs, ProductService products, SalesService sales, CustomerService customers,
        SettingsService settings, UserService users, Session session, PrintService print, INavigationService navigation)
        : base(dialogs)
    {
        _products = products;
        _sales = sales;
        _customers = customers;
        _settings = settings;
        _users = users;
        _print = print;
        _navigation = navigation;
        Session = session;

        Items.CollectionChanged += OnItemsChanged;
        settings.SettingsChanged += (_, _) => Recalculate();
        session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Session.CurrentUser)) ResetSale();
        };
    }

    public string Title => "Register";
    public Session Session { get; }

    /// <summary>Asks the view to put the cursor back in the scan box.</summary>
    public event EventHandler? FocusSearchRequested;

    public ObservableCollection<CartItemViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveItemCommand), nameof(LineDiscountCommand), nameof(SetQuantityCommand))]
    public partial CartItemViewModel? SelectedItem { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial List<ProductVariant> SearchResults { get; set; } = [];

    [ObservableProperty]
    public partial ProductVariant? SelectedResult { get; set; }

    [ObservableProperty]
    public partial bool IsSearchOpen { get; set; }

    /// <summary>Shown in the results panel when nothing matches.</summary>
    [ObservableProperty]
    public partial string? SearchMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomer))]
    public partial Customer? Customer { get; set; }

    public bool HasCustomer => Customer is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CartDiscountDisplay))]
    public partial DiscountType CartDiscountType { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CartDiscountDisplay))]
    public partial decimal CartDiscountValue { get; set; }

    [ObservableProperty]
    public partial CartTotals Totals { get; private set; } = CartTotals.Empty;

    [ObservableProperty]
    public partial int HeldCount { get; private set; }

    public int ItemCount => Items.Sum(i => i.Quantity);
    public bool HasItems => Items.Count > 0;

    public string TaxLabel => _settings.Current.PricesIncludeTax
        ? $"Tax included ({_settings.Current.TaxRate:0.##}%)"
        : $"Tax ({_settings.Current.TaxRate:0.##}%)";

    public string CartDiscountDisplay => CartDiscountType switch
    {
        DiscountType.Percent => $"Cart discount {CartDiscountValue:0.##}%",
        DiscountType.Amount => $"Cart discount {CurrencyFormat.Format(CartDiscountValue)}",
        _ => "",
    };

    public async Task OnNavigatedToAsync()
    {
        await RefreshHeldCountAsync();
        FocusSearchRequested?.Invoke(this, EventArgs.Empty);
    }

    // ---- Adding items -----------------------------------------------------------------------

    private readonly LatestSearch _search = new();

    /// <summary>Shows matching items while the cashier types, without adding anything.</summary>
    partial void OnSearchTextChanged(string value)
    {
        var text = value.Trim();
        if (text.Length < 2)
        {
            _search.Cancel();
            if (IsSearchOpen && text.Length == 0) HideResults();
            return;
        }
        _ = LiveSearchAsync(text);
    }

    private async Task LiveSearchAsync(string text)
    {
        try
        {
            await _search.RunAsync(ct => _products.SearchVariantsAsync(text, ct: ct), results => ShowResults(text, results));
        }
        catch (Exception ex)
        {
            Dialogs.Error("Product search failed.", ex);
        }
    }

    /// <summary>
    /// Enter or a barcode scan: an exact barcode/SKU or a single match is added straight away, otherwise the
    /// matches are listed. Runs even while an earlier search is still going (that one is cancelled).
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task SearchAsync()
    {
        var text = SearchText.Trim();
        if (text.Length == 0) return;
        try
        {
            await _search.RunNowAsync(
                async ct => await _products.FindByCodeAsync(text, ct) is { } exact ? [exact] : await _products.SearchVariantsAsync(text, ct: ct),
                results =>
                {
                    if (results.Count == 1) AddVariant(results[0]);
                    else ShowResults(text, results);
                });
        }
        catch (Exception ex)
        {
            Dialogs.Error("Product search failed.", ex);
        }
    }

    private void ShowResults(string text, List<ProductVariant> results)
    {
        SearchResults = results;
        SelectedResult = results.FirstOrDefault();
        SearchMessage = results.Count == 0 ? $"No products match \"{text}\"." : null;
        IsSearchOpen = true;
    }

    private void HideResults()
    {
        IsSearchOpen = false;
        SearchResults = [];
        SearchMessage = null;
    }

    [RelayCommand]
    private void AddResult(ProductVariant? variant)
    {
        variant ??= SelectedResult;
        if (variant is null) return;
        AddVariant(variant);
    }

    [RelayCommand]
    private void CloseSearch()
    {
        _search.Cancel();
        HideResults();
        FocusSearchRequested?.Invoke(this, EventArgs.Empty);
    }

    private void AddVariant(ProductVariant variant)
    {
        var existing = Items.FirstOrDefault(i => i.VariantId == variant.Id && i.DiscountType == DiscountType.None);
        var newQty = (existing?.Quantity ?? 0) + 1;
        if (!CheckStock(variant.StockQuantity, newQty, variant.DisplayName)) return;

        if (existing is not null)
        {
            existing.Quantity = newQty;
            SelectedItem = existing;
        }
        else
        {
            var item = new CartItemViewModel(variant);
            Items.Add(item);
            SelectedItem = item;
        }

        SearchText = "";
        HideResults();
        FocusSearchRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool CheckStock(int onHand, int wanted, string name)
    {
        if (wanted <= onHand || _settings.Current.AllowNegativeStock) return true;
        Dialogs.Warning(onHand <= 0 ? $"{name} is out of stock." : $"Only {onHand} x {name} in stock.");
        return false;
    }

    // ---- Cart editing -----------------------------------------------------------------------

    [RelayCommand]
    private void Increase(CartItemViewModel? item)
    {
        item ??= SelectedItem;
        if (item is null || !CheckStock(item.StockOnHand, item.Quantity + 1, item.ProductName)) return;
        item.Quantity++;
    }

    [RelayCommand]
    private void Decrease(CartItemViewModel? item)
    {
        item ??= SelectedItem;
        if (item is null) return;
        if (item.Quantity > 1) item.Quantity--;
        else Items.Remove(item);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RemoveItem(CartItemViewModel? item)
    {
        item ??= SelectedItem;
        if (item is null) return;
        var index = Items.IndexOf(item);
        Items.Remove(item);
        SelectedItem = Items.Count == 0 ? null : Items[Math.Min(index, Items.Count - 1)];
        FocusSearchRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void SetQuantity()
    {
        if (SelectedItem is not { } item) return;
        var qty = Dialogs.PromptInt("Quantity", $"Quantity for {item.ProductName} ({item.VariantDescription})", item.Quantity);
        if (qty is null) return;
        if (qty <= 0)
        {
            Items.Remove(item);
            return;
        }
        if (CheckStock(item.StockOnHand, qty.Value, item.ProductName)) item.Quantity = qty.Value;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void LineDiscount()
    {
        if (SelectedItem is not { } item) return;
        var dialog = new DiscountViewModel(Dialogs, $"Discount: {item.ProductName}", item.UnitPrice * item.Quantity, item.DiscountType, item.DiscountValue);
        if (!Dialogs.ShowDialog(dialog)) return;

        var (oldType, oldValue) = (item.DiscountType, item.DiscountValue);
        item.DiscountType = dialog.DiscountType;
        item.DiscountValue = dialog.Value;
        if (!EnsureDiscountApproved())
        {
            item.DiscountType = oldType;
            item.DiscountValue = oldValue;
        }
    }

    [RelayCommand]
    private void CartDiscount()
    {
        if (!HasItems) return;
        var dialog = new DiscountViewModel(Dialogs, "Cart discount", Totals.Subtotal - Totals.LineDiscounts, CartDiscountType, CartDiscountValue);
        if (!Dialogs.ShowDialog(dialog)) return;

        var (oldType, oldValue) = (CartDiscountType, CartDiscountValue);
        CartDiscountType = dialog.DiscountType;
        CartDiscountValue = dialog.Value;
        Recalculate();
        if (!EnsureDiscountApproved())
        {
            CartDiscountType = oldType;
            CartDiscountValue = oldValue;
            Recalculate();
        }
    }

    private bool HasSelection() => SelectedItem is not null;

    /// <summary>Cashiers need a manager's credentials for discounts above the configured limit.</summary>
    private bool EnsureDiscountApproved()
    {
        Recalculate();
        if (Session.Can(Permission.OverrideDiscountLimit)) return true;

        var limit = _settings.Current.MaxCashierDiscountPercent;
        var worst = Math.Max(
            CartCalculator.DiscountPercent(Totals.Subtotal, Totals.DiscountTotal),
            Totals.Lines.Select(l => CartCalculator.DiscountPercent(l.Gross, l.Discount)).DefaultIfEmpty(0).Max());
        if (worst <= limit) return true;
        if (_approvedByUserId is not null && worst <= _approvedDiscountPercent) return true;

        var approval = new ManagerApprovalViewModel(Dialogs, _users,
            $"A discount of {worst:0.##}% exceeds your limit of {limit:0.##}%.", Permission.OverrideDiscountLimit);
        if (!Dialogs.ShowDialog(approval) || approval.ApprovedBy is null) return false;

        _approvedByUserId = approval.ApprovedBy.Id;
        _approvedDiscountPercent = worst;
        return true;
    }

    // ---- Customer ---------------------------------------------------------------------------

    [RelayCommand]
    private void SelectCustomer()
    {
        var picker = new CustomerPickerViewModel(Dialogs, _customers);
        if (Dialogs.ShowDialog(picker) && picker.Selected is not null) Customer = picker.Selected;
        FocusSearchRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ClearCustomer() => Customer = null;

    // ---- Hold / resume ----------------------------------------------------------------------

    [RelayCommand]
    private Task HoldAsync() => RunAsync(async () =>
    {
        if (!HasItems) return;
        var label = Dialogs.Prompt("Hold sale", "Label for this sale (e.g. customer name or fitting room):",
            Customer?.FullName ?? $"Sale {DateTime.Now:HH:mm}");
        if (label is null) return;

        var cart = new HeldCart(
            Items.Select(i => new HeldCartLine(i.VariantId, i.Quantity, i.DiscountType, i.DiscountValue)).ToList(),
            CartDiscountType, CartDiscountValue, Customer?.Id);
        await _sales.HoldAsync(label, Session.User.Id, cart);
        ResetSale();
        await RefreshHeldCountAsync();
    });

    [RelayCommand]
    private Task ResumeAsync() => RunAsync(async () =>
    {
        if (HasItems && !Dialogs.Confirm("The current cart will be replaced. Continue?")) return;

        var picker = new HeldSalesViewModel(Dialogs, _sales);
        if (!Dialogs.ShowDialog(picker) || picker.Chosen is null)
        {
            await RefreshHeldCountAsync();
            return;
        }

        var cart = await _sales.ResumeAsync(picker.Chosen.Id);
        var variants = (await _products.GetVariantsAsync(cart.Lines.Select(l => l.VariantId))).ToDictionary(v => v.Id);

        ResetSale();
        var missing = 0;
        foreach (var line in cart.Lines)
        {
            if (!variants.TryGetValue(line.VariantId, out var v) || !v.IsActive)
            {
                missing++;
                continue;
            }
            Items.Add(new CartItemViewModel(v, line.Quantity) { DiscountType = line.DiscountType, DiscountValue = line.DiscountValue });
        }
        CartDiscountType = cart.CartDiscountType;
        CartDiscountValue = cart.CartDiscountValue;
        if (cart.CustomerId is { } customerId) Customer = await _customers.GetAsync(customerId);
        Recalculate();
        await RefreshHeldCountAsync();

        if (missing > 0) Dialogs.Warning($"{missing} item(s) are no longer available and were skipped.");
    });

    [RelayCommand]
    private void ClearCart()
    {
        if (!HasItems && Customer is null) return;
        if (Dialogs.Confirm("Clear the current sale?")) ResetSale();
    }

    // ---- Checkout ---------------------------------------------------------------------------

    [RelayCommand]
    private async Task PayAsync()
    {
        if (!HasItems) return;

        if (!Session.HasOpenShift)
        {
            if (Dialogs.Confirm("You need an open cash drawer shift before taking payments.\n\nGo to the Cash Drawer screen now?"))
                await _navigation.NavigateToAsync<ShiftViewModel>();
            return;
        }

        if (!EnsureDiscountApproved()) return;

        var payment = new PaymentViewModel(Dialogs, Totals.Total, Customer, _settings.Current);
        if (!Dialogs.ShowDialog(payment)) return;

        Sale? sale = null;
        var ok = await RunAsync(async () =>
        {
            sale = await _sales.CompleteSaleAsync(new CheckoutRequest
            {
                UserId = Session.User.Id,
                ShiftId = Session.CurrentShift?.Id,
                CustomerId = Customer?.Id,
                Lines = Items.Select(i => new CheckoutLine(i.VariantId, i.Quantity, i.DiscountType, i.DiscountValue)).ToList(),
                CartDiscountType = CartDiscountType,
                CartDiscountValue = CartDiscountValue,
                Payments = payment.Payments,
                ApprovedByUserId = _approvedByUserId,
            });
        });
        if (!ok || sale is null) return;

        ResetSale();
        var receipt = Core.Receipts.ReceiptFormatter.Format(ReceiptBuilder.FromSale(sale, _settings.Current), _settings.Current.ReceiptWidth);
        var title = sale.ChangeGiven > 0 ? $"Change due: {CurrencyFormat.Format(sale.ChangeGiven)}" : $"Sale {sale.ReceiptNumber} complete";
        Dialogs.ShowDialog(new TextPreviewViewModel(Dialogs, _print, title, receipt, "New sale"));
        FocusSearchRequested?.Invoke(this, EventArgs.Empty);
    }

    // ---- Internals --------------------------------------------------------------------------

    private void ResetSale()
    {
        Items.Clear();
        Customer = null;
        CartDiscountType = DiscountType.None;
        CartDiscountValue = 0;
        SearchText = "";
        HideResults();
        _approvedByUserId = null;
        _approvedDiscountPercent = 0;
        Recalculate();
    }

    private async Task RefreshHeldCountAsync()
    {
        try
        {
            HeldCount = (await _sales.GetHeldAsync()).Count;
        }
        catch
        {
            HeldCount = 0;
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (CartItemViewModel item in e.OldItems) item.PropertyChanged -= OnItemPropertyChanged;
        if (e.NewItems is not null)
            foreach (CartItemViewModel item in e.NewItems) item.PropertyChanged += OnItemPropertyChanged;
        if (e.Action == NotifyCollectionChangedAction.Reset)
            SelectedItem = null;
        Recalculate();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CartItemViewModel.Quantity) or nameof(CartItemViewModel.DiscountType) or nameof(CartItemViewModel.DiscountValue))
            Recalculate();
    }

    private bool _recalculating;

    private void Recalculate()
    {
        if (_recalculating) return;
        _recalculating = true;
        try
        {
            var s = _settings.Current;
            Totals = Items.Count == 0
                ? CartTotals.Empty
                : CartCalculator.Calculate(Items.Select(i => i.ToInput()).ToList(), CartDiscountType, CartDiscountValue, s.TaxRate, s.PricesIncludeTax);
            for (var i = 0; i < Items.Count; i++) Items[i].Apply(Totals.Lines[i]);
            OnPropertyChanged(nameof(ItemCount));
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(TaxLabel));
        }
        finally
        {
            _recalculating = false;
        }
    }
}
