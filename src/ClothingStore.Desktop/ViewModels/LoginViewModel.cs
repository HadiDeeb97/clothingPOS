using ClothingStore.Core.Localization;
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

    /// <summary>Raised when the screen should close and reopen in <see cref="RequestedLanguage"/>.</summary>
    public event EventHandler? LanguageRequested;

    public string StoreName => settings.Current.StoreName;
    public Branding Branding => Branding.Instance;
    public bool IsArabic => Loc.IsRightToLeft;

    /// <summary>Set when the user switched language on this screen.</summary>
    public string? RequestedLanguage { get; private set; }

    /// <summary>First-run hint, shown only while the default admin password is still in place.</summary>
    [ObservableProperty]
    public partial string? Hint { get; set; }

    public async Task InitializeAsync()
    {
        try
        {
            if (await users.IsFirstRunAsync())
                Hint = Loc.T("Login.FirstRunHint", DatabaseInitializer.DefaultAdminUser, DatabaseInitializer.DefaultAdminPassword);
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
    private void SetLanguage(string language)
    {
        if (Loc.Normalize(language) == Loc.Language) return;
        RequestedLanguage = Loc.Normalize(language);
        LanguageRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private Task SignInAsync() => RunAsync(async () =>
    {
        ErrorMessage = null;
        var user = await users.AuthenticateAsync(Username, Password);
        if (user is null)
        {
            ErrorMessage = Loc.T("Login.Invalid");
            return;
        }

        session.CurrentUser = user;
        session.CurrentShift = await shifts.GetOpenShiftAsync(user.Id);
        SignedIn?.Invoke(this, EventArgs.Empty);
    });
}
