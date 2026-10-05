using ClothingStore.Data.Seeding;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClothingStore.Desktop.ViewModels;

public sealed partial class LoginViewModel(IDialogService dialogs, UserService users, ShiftService shifts, SettingsService settings, Session session)
    : ViewModelBase(dialogs)
{
    public event EventHandler? SignedIn;

    public string StoreName => settings.Current.StoreName;

    /// <summary>First-run hint, shown only while the default admin password is still in place.</summary>
    [ObservableProperty]
    public partial string? Hint { get; set; }

    public async Task InitializeAsync()
    {
        try
        {
            if (await users.IsFirstRunAsync())
                Hint = $"First run: sign in as '{DatabaseInitializer.DefaultAdminUser}' / '{DatabaseInitializer.DefaultAdminPassword}' " +
                       "(you will be asked to choose a new password). Demo data also adds manager/manager123 and cashier/cashier123.";
        }
        catch
        {
            // The hint is a convenience only.
        }
    }

    [ObservableProperty]
    public partial string Username { get; set; } = "";

    public string Password { get; set; } = "";

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [RelayCommand]
    private Task SignInAsync() => RunAsync(async () =>
    {
        ErrorMessage = null;
        var user = await users.AuthenticateAsync(Username, Password);
        if (user is null)
        {
            ErrorMessage = "Invalid username or password.";
            return;
        }

        session.CurrentUser = user;
        session.CurrentShift = await shifts.GetOpenShiftAsync(user.Id);
        SignedIn?.Invoke(this, EventArgs.Empty);
    });
}
