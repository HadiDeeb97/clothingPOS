using System.Collections.ObjectModel;
using System.Windows.Threading;
using ClothingStore.Core.Localization;
using ClothingStore.Core.Security;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Infrastructure;
using ClothingStore.Desktop.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClothingStore.Desktop.ViewModels;

public sealed partial class NavItem(string title, string glyph, Type pageType, Func<Task> navigate) : ObservableObject
{
    public string Title { get; } = title;
    public string Glyph { get; } = glyph;
    public Type PageType { get; } = pageType;
    public IAsyncRelayCommand NavigateCommand { get; } = new AsyncRelayCommand(navigate);

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Number shown in a red bubble (e.g. new online orders); 0 hides it.</summary>
    [ObservableProperty]
    public partial int Badge { get; set; }
}

public sealed class NavGroup(string title, IReadOnlyList<NavItem> items)
{
    public string Title { get; } = title;
    public IReadOnlyList<NavItem> Items { get; } = items;
}

public sealed partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly NavigationService _navigation;
    private readonly SettingsService _settings;
    private readonly UserService _users;
    private readonly BrandingService _branding;
    private readonly OnlineOrderService _orders;
    private readonly DispatcherTimer _clock;
    private readonly NavItem? _ordersNav;

    public MainViewModel(
        IDialogService dialogs, NavigationService navigation, Session session, SettingsService settings, UserService users,
        BrandingService branding, OnlineOrderService orders)
        : base(dialogs)
    {
        _navigation = navigation;
        _settings = settings;
        _users = users;
        _branding = branding;
        _orders = orders;
        Session = session;
        IsSidebarCollapsed = LocalPreferences.Current.SidebarCollapsed;

        AddGroup("Nav.Group.Sell",
            Nav<SalesViewModel>("Nav.Register", "", Permission.Sell),
            Nav<ReturnsViewModel>("Nav.Returns", "", Permission.ProcessReturns),
            _ordersNav = Nav<OnlineOrdersViewModel>("Nav.OnlineOrders", "\uE7BF", Permission.Sell),
            Nav<SalesHistoryViewModel>("Nav.SalesHistory", "", Permission.Sell),
            Nav<ShiftViewModel>("Nav.CashDrawer", "", Permission.Sell));
        AddGroup("Nav.Group.People",
            Nav<CustomersViewModel>("Nav.Customers", "", Permission.ManageCustomers));
        AddGroup("Nav.Group.Catalog",
            Nav<ProductsViewModel>("Nav.Products", "", Permission.ViewProducts),
            Nav<InventoryViewModel>("Nav.Inventory", "", Permission.ManageInventory),
            Nav<CategoriesViewModel>("Nav.Categories", "", Permission.ManageProducts));
        AddGroup("Nav.Group.Purchasing",
            Nav<PurchaseOrdersViewModel>("Nav.PurchaseOrders", "", Permission.ManagePurchasing),
            Nav<SuppliersViewModel>("Nav.Suppliers", "", Permission.ManagePurchasing));
        AddGroup("Nav.Group.Insights",
            Nav<ReportsViewModel>("Nav.Reports", "", Permission.ViewReports));
        AddGroup("Nav.Group.Admin",
            Nav<UsersViewModel>("Nav.Users", "", Permission.ManageUsers),
            Nav<SettingsViewModel>("Nav.Settings", "", Permission.ManageSettings));

        _navigation.Navigated += OnNavigated;
        _navigation.PropertyChanged += OnNavigationPropertyChanged;
        _settings.SettingsChanged += OnSettingsChanged;
        _orders.Changed += OnOrdersChanged;
        _ = RefreshOrderBadgeAsync();

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _clock.Tick += OnClockTick;
        _clock.Start();
    }

    public event EventHandler? SignOutRequested;

    /// <summary>The user picked another language; the app rebuilds its windows.</summary>
    public event EventHandler<string>? LanguageChangeRequested;

    /// <summary>The user asked for default panel sizes; the app rebuilds the window after forgetting them.</summary>
    public event EventHandler? LayoutResetRequested;

    public Session Session { get; }
    public ObservableCollection<NavGroup> NavGroups { get; } = [];
    public IEnumerable<NavItem> NavItems => NavGroups.SelectMany(g => g.Items);
    public IPageViewModel? CurrentPage => _navigation.CurrentPage;
    public string StoreName => _settings.Current.StoreName;
    public DateTime Now => DateTime.Now;
    public Branding Branding => Branding.Instance;
    public string RoleDisplay => Session.CurrentUser is { } u ? Loc.EnumText(u.Role) : "";
    public bool IsArabic => Loc.IsRightToLeft;

    /// <summary>LBP rate chip in the top bar.</summary>
    public bool ShowRate => _settings.Current.ActiveLbpRate > 0;
    public string RateText => Loc.T("Rate.Short", _settings.Current.LbpRate.ToString("N0"));
    public bool CanChangeRate => Session.Can(Permission.ChangeExchangeRate);
    public bool CanChangeLogo => Session.Can(Permission.ManageBranding);

    [ObservableProperty]
    public partial bool IsSidebarCollapsed { get; set; }

    [ObservableProperty]
    public partial bool IsUserMenuOpen { get; set; }

    partial void OnIsSidebarCollapsedChanged(bool value)
    {
        LocalPreferences.Current.SidebarCollapsed = value;
        LocalPreferences.Current.Save();
    }

    /// <summary>Opens the first screen this user may use (the register for sellers).</summary>
    public Task StartAsync(Type? page = null)
    {
        var start = NavItems.FirstOrDefault(i => i.PageType == page) ?? NavItems.FirstOrDefault();
        return start?.NavigateCommand.ExecuteAsync(null) ?? Task.CompletedTask;
    }

    private NavItem? Nav<TPage>(string titleKey, string glyph, Permission permission) where TPage : IPageViewModel =>
        Session.Can(permission) ? new NavItem(Loc.T(titleKey), glyph, typeof(TPage), () => _navigation.NavigateToAsync<TPage>()) : null;

    private void AddGroup(string titleKey, params NavItem?[] items)
    {
        var visible = items.OfType<NavItem>().ToList();
        if (visible.Count > 0) NavGroups.Add(new NavGroup(Loc.T(titleKey), visible));
    }

    private void OnNavigated(object? sender, EventArgs e)
    {
        var type = _navigation.CurrentPage?.GetType();
        foreach (var item in NavItems) item.IsSelected = item.PageType == type;
    }

    private void OnNavigationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NavigationService.CurrentPage)) OnPropertyChanged(nameof(CurrentPage));
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(StoreName));
        OnPropertyChanged(nameof(ShowRate));
        OnPropertyChanged(nameof(RateText));
    }

    private int _ticks;

    private async void OnClockTick(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Now));

        // Every minute, pick up a rate or logo changed on another till.
        if (++_ticks % 4 != 0) return;
        try
        {
            if (await _settings.RefreshCurrencyAsync())
                Dialogs.Toast(Loc.T("Rate.ChangedElsewhere", _settings.Current.LbpRate.ToString("N0")));
            await Branding.Instance.RefreshAsync(_branding, onlyIfChanged: true);
            await RefreshOrderBadgeAsync();
        }
        catch
        {
            // Offline for a moment: the next tick tries again, and checkout re-checks the rate anyway.
        }
    }

    private void OnOrdersChanged(object? sender, EventArgs e) =>
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => _ = RefreshOrderBadgeAsync());

    /// <summary>Red bubble on "Online orders": orders taken but not yet confirmed.</summary>
    private async Task RefreshOrderBadgeAsync()
    {
        if (_ordersNav is null) return;
        try
        {
            _ordersNav.Badge = (await _orders.GetCountsAsync()).New;
        }
        catch
        {
            // the next tick tries again
        }
    }

    [RelayCommand]
    private void ChangeLogo()
    {
        IsUserMenuOpen = false;
        Dialogs.ShowDialog(new StoreLogoViewModel(Dialogs, _branding, Session));
    }

    [RelayCommand]
    private void ChangeRate()
    {
        if (Dialogs.ShowDialog(new ExchangeRateViewModel(Dialogs, _settings, Session)))
            Dialogs.Toast(Loc.T("Rate.Changed", _settings.Current.LbpRate.ToString("N0")));
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    [RelayCommand]
    private void ChangePassword()
    {
        IsUserMenuOpen = false;
        if (Dialogs.ShowDialog(new ChangePasswordViewModel(Dialogs, _users, Session, forced: false)))
            Dialogs.Toast(Loc.T("Shell.PasswordChanged"));
    }

    [RelayCommand]
    private async Task SetLanguageAsync(string language)
    {
        IsUserMenuOpen = false;
        if (Loc.Normalize(language) == Loc.Language) return;
        try
        {
            await _users.SetPreferredLanguageAsync(Session.User.Id, language);
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Common.SomethingWentWrong"), ex);
            return;
        }
        LanguageChangeRequested?.Invoke(this, Loc.Normalize(language));
    }

    [RelayCommand]
    private void ResetLayout()
    {
        IsUserMenuOpen = false;
        if (Dialogs.Confirm(Loc.T("Shell.ResetLayoutConfirm"))) LayoutResetRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SignOut()
    {
        IsUserMenuOpen = false;
        if (Dialogs.Confirm(Loc.T("Shell.SignOutConfirm"))) SignOutRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _clock.Stop();
        _clock.Tick -= OnClockTick;
        _navigation.Navigated -= OnNavigated;
        _navigation.PropertyChanged -= OnNavigationPropertyChanged;
        _settings.SettingsChanged -= OnSettingsChanged;
        _orders.Changed -= OnOrdersChanged;
    }
}
