using System.Windows;
using System.Windows.Threading;
using ClothingStore.Data;
using ClothingStore.Data.Seeding;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Converters;
using ClothingStore.Desktop.Infrastructure;
using ClothingStore.Desktop.Services;
using ClothingStore.Desktop.ViewModels;
using ClothingStore.Desktop.ViewModels.Dialogs;
using ClothingStore.Desktop.Views;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClothingStore.Desktop;

public partial class App : Application
{
    private IHost? _host;
    private bool _signingOut;

    public static IServiceProvider Services { get; private set; } = null!;
    /// <summary>Server and database name, for display (never includes credentials).</summary>
    public static string DatabaseName { get; private set; } = "";

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = e.Args,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

        var connectionString = builder.Configuration.GetConnectionString("Pos");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            MessageBox.Show("No database is configured. Set ConnectionStrings:Pos in appsettings.json.",
                "Clothing Store POS", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        try
        {
            var csb = new SqlConnectionStringBuilder(connectionString);
            DatabaseName = $"{csb.InitialCatalog} on {csb.DataSource}";
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show($"The database connection string in appsettings.json is not valid:\n\n{ex.Message}",
                "Clothing Store POS", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        builder.Services.AddPosData(connectionString);
        ConfigureServices(builder.Services);
        _host = builder.Build();
        Services = _host.Services;

        try
        {
            var seedDemo = builder.Configuration.GetValue("Pos:SeedDemoData", true);
            await Services.GetRequiredService<DatabaseInitializer>().InitializeAsync(seedDemo);
            var settings = Services.GetRequiredService<SettingsService>();
            CurrencyFormat.Symbol = (await settings.GetAsync()).CurrencySymbol;
            settings.SettingsChanged += (_, _) => CurrencyFormat.Symbol = settings.Current.CurrencySymbol;
            await _host.StartAsync(); // starts the automatic backup worker
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not connect to the database:\n{DatabaseName}\n\n{ex.GetBaseException().Message}\n\n" +
                            "Check that SQL Server is running and reachable from this PC, and that ConnectionStrings:Pos in appsettings.json is correct.",
                "Clothing Store POS", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        ShowLogin();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<Session>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());
        services.AddSingleton<PrintService>();
        services.AddHostedService<AutoBackupWorker>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainViewModel>();

        // The register keeps its cart while you visit other screens.
        services.AddSingleton<SalesViewModel>();
        services.AddTransient<ReturnsViewModel>();
        services.AddTransient<SalesHistoryViewModel>();
        services.AddTransient<CustomersViewModel>();
        services.AddTransient<ProductsViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<PurchaseOrdersViewModel>();
        services.AddTransient<SuppliersViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<ShiftViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<SettingsViewModel>();
    }

    private void ShowLogin()
    {
        var session = Services.GetRequiredService<Session>();
        var login = new LoginWindow(Services.GetRequiredService<LoginViewModel>());
        if (login.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        if (session.User.MustChangePassword)
        {
            var change = new ChangePasswordViewModel(
                Services.GetRequiredService<IDialogService>(), Services.GetRequiredService<UserService>(), session, forced: true);
            if (!Services.GetRequiredService<IDialogService>().ShowDialog(change))
            {
                session.SignOut();
                ShowLogin();
                return;
            }
        }

        var vm = Services.GetRequiredService<MainViewModel>();
        var main = new MainWindow { DataContext = vm };
        vm.SignOutRequested += (_, _) =>
        {
            _signingOut = true;
            main.Close();
        };
        main.Closed += (_, _) =>
        {
            vm.Dispose();
            if (_signingOut)
            {
                _signingOut = false;
                Services.GetRequiredService<NavigationService>().Reset();
                session.SignOut();
                Dispatcher.BeginInvoke(ShowLogin);
            }
            else
            {
                Shutdown();
            }
        };
        MainWindow = main;
        main.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"An unexpected error occurred:\n\n{e.Exception.GetBaseException().Message}",
            "Clothing Store POS", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
