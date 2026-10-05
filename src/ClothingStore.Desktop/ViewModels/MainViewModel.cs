using System.Collections.ObjectModel;
using System.Windows.Threading;
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
}

public sealed partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly NavigationService _navigation;
    private readonly SettingsService _settings;
    private readonly UserService _users;
    private readonly DispatcherTimer _clock;

    public MainViewModel(IDialogService dialogs, NavigationService navigation, Session session, SettingsService settings, UserService users)
        : base(dialogs)
    {
        _navigation = navigation;
        _settings = settings;
        _users = users;
        Session = session;

        AddNav<SalesViewModel>("Register", "\uE7BF", Permission.Sell);
        AddNav<ReturnsViewModel>("Returns", "\uE7A7", Permission.ProcessReturns);
        AddNav<SalesHistoryViewModel>("Sales History", "\uE81C", Permission.Sell);
        AddNav<CustomersViewModel>("Customers", "\uE716", Permission.ManageCustomers);
        AddNav<ProductsViewModel>("Products", "\uE8EC", Permission.ViewProducts);
        AddNav<InventoryViewModel>("Inventory", "\uE7B8", Permission.ManageInventory);
        AddNav<PurchaseOrdersViewModel>("Purchasing", "\uE719", Permission.ManagePurchasing);
        AddNav<SuppliersViewModel>("Suppliers", "\uE77B", Permission.ManagePurchasing);
        AddNav<CategoriesViewModel>("Categories", "\uE8FD", Permission.ManageProducts);
        AddNav<ShiftViewModel>("Cash Drawer", "\uE825", Permission.Sell);
        AddNav<ReportsViewModel>("Reports", "\uE9D2", Permission.ViewReports);
        AddNav<UsersViewModel>("Users", "\uE7EF", Permission.ManageUsers);
        AddNav<SettingsViewModel>("Settings", "\uE713", Permission.ManageSettings);

        _navigation.Navigated += OnNavigated;
        _navigation.PropertyChanged += OnNavigationPropertyChanged;
        _settings.SettingsChanged += OnSettingsChanged;

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _clock.Tick += (_, _) => OnPropertyChanged(nameof(Now));
        _clock.Start();

        // Land on the register for sellers, otherwise on the first screen they may use.
        var start = NavItems.FirstOrDefault();
        if (start is not null) _ = start.NavigateCommand.ExecuteAsync(null);
    }

    public event EventHandler? SignOutRequested;

    public Session Session { get; }
    public ObservableCollection<NavItem> NavItems { get; } = [];
    public IPageViewModel? CurrentPage => _navigation.CurrentPage;
    public string StoreName => _settings.Current.StoreName;
    public DateTime Now => DateTime.Now;

    private void AddNav<TPage>(string title, string glyph, Permission permission) where TPage : IPageViewModel
    {
        if (!Session.Can(permission)) return;
        NavItems.Add(new NavItem(title, glyph, typeof(TPage), () => _navigation.NavigateToAsync<TPage>()));
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

    private void OnSettingsChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(StoreName));

    [RelayCommand]
    private void ChangePassword()
    {
        if (Dialogs.ShowDialog(new ChangePasswordViewModel(Dialogs, _users, Session, forced: false)))
            Dialogs.Info("Your password has been changed.");
    }

    [RelayCommand]
    private void SignOut()
    {
        if (Dialogs.Confirm("Sign out of the register?")) SignOutRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _clock.Stop();
        _navigation.Navigated -= OnNavigated;
        _navigation.PropertyChanged -= OnNavigationPropertyChanged;
        _settings.SettingsChanged -= OnSettingsChanged;
    }
}
