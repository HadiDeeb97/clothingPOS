using System.Windows;
using System.Windows.Threading;
using ClothingStore.Core.Localization;
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
    private SplashWindow? _splash;

    public static IServiceProvider Services { get; private set; } = null!;
    /// <summary>Server and database name, for display (never includes credentials).</summary>
    public static string DatabaseName { get; private set; } = "";

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        Loc.SetLanguage(LocalPreferences.Current.Language);
        _splash = new SplashWindow();
        _splash.Show();

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = e.Args,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

        var connectionString = builder.Configuration.GetConnectionString("Pos");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            StartupError(Loc.T("Startup.NoConnectionString"));
            return;
        }
        try
        {
            var csb = new SqlConnectionStringBuilder(connectionString);
            DatabaseName = Loc.T("Startup.DatabaseOn", csb.InitialCatalog, csb.DataSource);
        }
        catch (ArgumentException ex)
        {
            StartupError(Loc.T("Startup.BadConnectionString", ex.Message));
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

            // Compile the screens' queries while the sign-in screen is up, so first visits are fast too.
            var warmUp = Services.GetRequiredService<QueryWarmUp>();
            _ = Task.Run(() => warmUp.RunAsync());
        }
        catch (Exception ex)
        {
            StartupError(Loc.T("Startup.CannotConnect", DatabaseName, ex.GetBaseException().Message));
            return;
        }

        CloseSplash();
        ShowLogin();
    }

    private void CloseSplash()
    {
        _splash?.Close();
        _splash = null;
        MainWindow = null; // the splash was the first window, so WPF made it the main window
    }

    private void StartupError(string message)
    {
        CloseSplash();
        MessageBox.Show(message, Loc.T("Shell.AppName"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK,
            Loc.IsRightToLeft ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : MessageBoxOptions.None);
        Shutdown(1);
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
        var dialogs = Services.GetRequiredService<IDialogService>();
        while (true)
        {
            var viewModel = Services.GetRequiredService<LoginViewModel>();
            var login = new LoginWindow(viewModel);
            var signedIn = login.ShowDialog() == true;
            if (viewModel.RequestedLanguage is { } language)
            {
                // The sign-in screen was switched to another language: show it again in that language.
                SetLanguage(language);
                continue;
            }
            if (!signedIn)
            {
                Shutdown();
                return;
            }
            break;
        }

        if (session.User.PreferredLanguage is { } preferred) SetLanguage(preferred);

        if (session.User.MustChangePassword)
        {
            var change = new ChangePasswordViewModel(dialogs, Services.GetRequiredService<UserService>(), session, forced: true);
            if (!dialogs.ShowDialog(change))
            {
                session.SignOut();
                ShowLogin();
                return;
            }
        }

        ShowMain(startPage: null);
    }

    private static void SetLanguage(string language)
    {
        Loc.SetLanguage(language);
        LocalPreferences.Current.Language = Loc.Language;
        LocalPreferences.Current.Save();
    }

    /// <summary>Opens the main window; it is rebuilt (without signing out) to change language or reset the layout.</summary>
    private void ShowMain(Type? startPage)
    {
        var session = Services.GetRequiredService<Session>();
        var vm = Services.GetRequiredService<MainViewModel>();
        var main = new MainWindow { DataContext = vm };
        var next = AfterMain.Exit;
        var resetLayout = false;

        vm.SignOutRequested += (_, _) =>
        {
            next = AfterMain.SignIn;
            main.Close();
        };
        vm.LanguageChangeRequested += (_, language) =>
        {
            SetLanguage(language);
            next = AfterMain.Rebuild;
            main.Close();
        };
        vm.LayoutResetRequested += (_, _) =>
        {
            resetLayout = true;
            next = AfterMain.Rebuild;
            main.Close();
        };
        main.Closed += (_, _) =>
        {
            var page = vm.CurrentPage?.GetType();
            vm.Dispose();
            switch (next)
            {
                case AfterMain.SignIn:
                    Services.GetRequiredService<NavigationService>().Reset();
                    session.SignOut();
                    Dispatcher.BeginInvoke(ShowLogin);
                    break;
                case AfterMain.Rebuild:
                    // Closing saved the current sizes; forget them only after that.
                    if (resetLayout) LayoutMemory.ResetAll();
                    Services.GetRequiredService<NavigationService>().Reset();
                    Dispatcher.BeginInvoke(() => ShowMain(page));
                    break;
                default:
                    Shutdown();
                    break;
            }
        };
        MainWindow = main;
        main.Show();
        _ = vm.StartAsync(startPage);
    }

    private enum AfterMain
    {
        Exit,
        SignIn,
        Rebuild,
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        try
        {
            Services.GetRequiredService<IDialogService>().Error(Loc.T("Startup.UnexpectedError"), e.Exception);
        }
        catch
        {
            MessageBox.Show(e.Exception.GetBaseException().Message, Loc.T("Shell.AppName"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
