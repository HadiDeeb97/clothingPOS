using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Security;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Infrastructure;
using ClothingStore.Desktop.Services;
using ClothingStore.Desktop.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClothingStore.Core.Localization;

namespace ClothingStore.Desktop.ViewModels;

/// <summary>
/// User accounts. Admins manage everyone; managers can add cashiers and edit, reset or deactivate cashier accounts.
/// </summary>
public sealed partial class UsersViewModel(IDialogService dialogs, UserService users, Session session) : ViewModelBase(dialogs), IPageViewModel
{
    public string Title => Loc.T("Nav.Users");

    /// <summary>Admins: every account. Managers: cashiers only.</summary>
    public bool CanManageAll => session.Can(Permission.ManageUsers);
    public string ScopeHint => CanManageAll ? Loc.T("Users.AdminHint") : Loc.T("Users.ManagerHint");

    [ObservableProperty]
    public partial List<User> Users { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleActiveText))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(ResetPasswordCommand), nameof(ToggleActiveCommand), nameof(DeleteCommand))]
    public partial User? SelectedUser { get; set; }

    public string ToggleActiveText => SelectedUser is { IsActive: false } ? Loc.T("Users.Activate") : Loc.T("Users.Deactivate");

    public async Task OnNavigatedToAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var selectedId = SelectedUser?.Id;
        try
        {
            Users = await users.GetAllAsync();
            SelectedUser = Users.FirstOrDefault(u => u.Id == selectedId) ?? Users.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Users.LoadFailed"), ex);
        }
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        var editor = new UserEditorViewModel(Dialogs, users, session, null);
        if (!Dialogs.ShowDialog(editor)) return;
        Dialogs.Toast(Loc.T("Users.Created", editor.Username));
        await RefreshAsync();
        SelectedUser = Users.FirstOrDefault(u => u.Username.Equals(editor.Username.Trim(), StringComparison.OrdinalIgnoreCase)) ?? SelectedUser;
    }

    /// <summary>Managers can only act on cashier accounts.</summary>
    private bool CanManageSelected() => SelectedUser is { } u && (CanManageAll || u.Role == UserRole.Cashier);
    private bool CanManageOther() => CanManageSelected() && SelectedUser!.Id != session.User.Id;

    [RelayCommand(CanExecute = nameof(CanManageSelected))]
    private async Task EditAsync()
    {
        if (SelectedUser is null) return;
        if (Dialogs.ShowDialog(new UserEditorViewModel(Dialogs, users, session, SelectedUser)))
        {
            Dialogs.Toast(SelectedUser.Id == session.User.Id ? Loc.T("Users.OwnAccountNextSignIn") : Loc.T("Common.Saved"));
            await RefreshAsync();
        }
    }

    /// <summary>For a forgotten password: a temporary one the person must change when they sign in.</summary>
    [RelayCommand(CanExecute = nameof(CanManageOther))]
    private async Task ResetPasswordAsync()
    {
        if (SelectedUser is not { } user) return;
        var temporary = Dialogs.Prompt(Loc.T("Users.ResetTitle"), Loc.T("Users.ResetPrompt", user.FullName, UserService.MinPasswordLength));
        if (temporary is null) return;
        if (await RunAsync(() => users.ResetPasswordAsync(user.Id, temporary, session.User.Id)))
            Dialogs.Info(Loc.T("Users.ResetDone", user.Username));
    }

    [RelayCommand(CanExecute = nameof(CanManageOther))]
    private async Task ToggleActiveAsync()
    {
        if (SelectedUser is not { } user) return;
        var activate = !user.IsActive;
        if (!activate && !Dialogs.Confirm(Loc.T("Users.DeactivateConfirm", user.FullName))) return;
        if (await RunAsync(() => users.SetActiveAsync(user.Id, activate, session.User.Id)))
        {
            Dialogs.Toast(Loc.T(activate ? "Users.Activated" : "Users.Deactivated", user.Username), activate ? ToastKind.Success : ToastKind.Info);
            await RefreshAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageOther))]
    private async Task DeleteAsync()
    {
        if (SelectedUser is not { } user) return;
        if (!Dialogs.Confirm(Loc.T("Users.DeleteConfirm", user.FullName))) return;
        if (await RunAsync(() => users.DeleteAsync(user.Id, session.User.Id)))
        {
            Dialogs.Toast(Loc.T("Users.Deleted", user.Username), ToastKind.Info);
            SelectedUser = null;
            await RefreshAsync();
        }
    }
}

public sealed partial class UserEditorViewModel : DialogViewModelBase
{
    private readonly UserService _users;
    private readonly Session _session;

    public UserEditorViewModel(IDialogService dialogs, UserService users, Session session, User? existing) : base(dialogs)
    {
        _users = users;
        _session = session;
        Id = existing?.Id ?? 0;
        Username = existing?.Username ?? "";
        FullName = existing?.FullName ?? "";
        Role = existing?.Role ?? UserRole.Cashier;
        IsActive = existing?.IsActive ?? true;
        MustChangePassword = existing is null;
        // Managers create cashiers only; the server checks this too.
        Roles = session.Can(Permission.ManageUsers) ? Enum.GetValues<UserRole>() : [UserRole.Cashier];
    }

    public override string Title => Id == 0 ? Loc.T("Users.New") : Loc.T("Users.Edit", Username);
    public int Id { get; }
    public bool IsNew => Id == 0;
    public UserRole[] Roles { get; }
    public bool CanChooseRole => Roles.Length > 1;

    [ObservableProperty] public partial string Username { get; set; }
    [ObservableProperty] public partial string FullName { get; set; }
    [ObservableProperty] public partial UserRole Role { get; set; }
    [ObservableProperty] public partial bool IsActive { get; set; }
    [ObservableProperty] public partial bool MustChangePassword { get; set; }

    public string Password { get; set; } = "";

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        await _users.SaveAsync(new User
        {
            Id = Id, Username = Username, FullName = FullName, Role = Role, IsActive = IsActive, MustChangePassword = MustChangePassword,
        }, string.IsNullOrEmpty(Password) ? null : Password, _session.User.Id);
        Close(true);
    });

    [RelayCommand]
    private void Cancel() => Close(false);
}

public sealed record LanguageOption(string Code, string Name);

/// <summary>A printer to pick in Settings; an empty <see cref="Name"/> means "ask every time" (Windows print dialog).</summary>
public sealed record PrinterChoice(string Name, string Display);

public sealed partial class SettingsViewModel(IDialogService dialogs, SettingsService settings, BackupService backup, Session session, BrandingService branding,
    PrintService print) : ViewModelBase(dialogs), IPageViewModel
{
    public string Title => Loc.T("Nav.Settings");
    public string DatabaseName => App.DatabaseName;

    /// <summary>Choices for the language printed on receipts.</summary>
    public IReadOnlyList<LanguageOption> ReceiptLanguages { get; } =
        Loc.Languages.Select(l => new LanguageOption(l, Loc.DisplayName(l))).ToList();

    [ObservableProperty]
    public partial StoreSettings Settings { get; set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoBackups))]
    public partial List<BackupRecord> Backups { get; set; } = [];

    public bool HasNoBackups => Backups.Count == 0;

    /// <summary>LBP rounding steps offered (amounts collected round up to it, change and refunds round down).</summary>
    public int[] LbpRoundings { get; } = [1, 100, 250, 500, 1_000, 5_000, 10_000];

    public string RateText => Loc.T("Rate.Short", Settings.LbpRate.ToString("N0"));
    public bool CanChangeRate => session.Can(Permission.ChangeExchangeRate);

    partial void OnSettingsChanged(StoreSettings value) => OnPropertyChanged(nameof(RateText));

    // ---- Printers (this PC only, kept in LocalPreferences) -------------------------------------

    [ObservableProperty]
    public partial List<PrinterChoice> Printers { get; set; } = [];

    [ObservableProperty]
    public partial string ReceiptPrinter { get; set; } = "";

    [ObservableProperty]
    public partial string LabelPrinter { get; set; } = "";

    [ObservableProperty]
    public partial int ReceiptCopies { get; set; } = 1;

    [RelayCommand]
    private void RefreshPrinters()
    {
        var installed = PrintService.InstalledPrinters();
        var list = new List<PrinterChoice> { new("", Loc.T("Print.AskEveryTime")) };
        list.AddRange(installed.Select(n => new PrinterChoice(n, n)));
        // A saved printer that is unplugged or renamed stays visible, so it isn't silently forgotten.
        foreach (var saved in new[] { LocalPreferences.Current.ReceiptPrinter, LocalPreferences.Current.LabelPrinter })
            if (!string.IsNullOrEmpty(saved) && list.All(c => !string.Equals(c.Name, saved, StringComparison.OrdinalIgnoreCase)))
                list.Add(new PrinterChoice(saved, Loc.T("Print.NotFound", saved)));
        Printers = list;
        ReceiptPrinter = LocalPreferences.Current.ReceiptPrinter ?? "";
        LabelPrinter = LocalPreferences.Current.LabelPrinter ?? "";
        ReceiptCopies = Math.Clamp(LocalPreferences.Current.ReceiptCopies, 1, 99);
    }

    private void SavePrinters()
    {
        var prefs = LocalPreferences.Current;
        prefs.ReceiptPrinter = string.IsNullOrEmpty(ReceiptPrinter) ? null : ReceiptPrinter;
        prefs.LabelPrinter = string.IsNullOrEmpty(LabelPrinter) ? null : LabelPrinter;
        prefs.ReceiptCopies = Math.Clamp(ReceiptCopies, 1, 99);
        prefs.Save();
    }

    [RelayCommand]
    private void TestReceiptPrinter()
    {
        SavePrinters();
        try
        {
            string[] lines = [Settings.StoreName, "", Loc.T("Print.TestLine"), DateTime.Now.ToString("g"), "", new string('-', 32)];
            if (print.PrintText(lines, Loc.T("Print.TestTitle")))
                Dialogs.Toast(Loc.T("Print.Sent", 1));
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Common.PrintFailed"), ex);
        }
    }

    public async Task OnNavigatedToAsync() => await ReloadAsync();

    [RelayCommand]
    private Task ReloadAsync() => RunAsync(async () =>
    {
        RefreshPrinters();
        Settings = (await settings.GetAsync()).Clone();
        Backups = await backup.GetRecentAsync();
    });

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        SavePrinters();
        await settings.SaveAsync(Settings.Clone());
        Settings = (await settings.GetAsync()).Clone();
        Dialogs.Toast(Loc.T("Settings.Saved"));
    });

    public Branding Branding => Branding.Instance;

    [RelayCommand]
    private void ChangeLogo() => Dialogs.ShowDialog(new StoreLogoViewModel(Dialogs, branding, session));

    [RelayCommand]
    private void ChangeRate()
    {
        if (!Dialogs.ShowDialog(new ExchangeRateViewModel(Dialogs, settings, session))) return;
        Settings.LbpRate = settings.Current.LbpRate; // the rest of the form keeps its unsaved edits
        OnPropertyChanged(nameof(RateText));
        Dialogs.Toast(Loc.T("Rate.Changed", settings.Current.LbpRate.ToString("N0")));
    }

    [RelayCommand]
    private async Task BackupAsync()
    {
        BackupRecord? record = null;
        var ok = await RunAsync(async () => record = await backup.BackupAsync(BackupKind.Manual, session.User.Id));
        Backups = await backup.GetRecentAsync(); // shows failures too
        if (ok)
            Dialogs.Info(Loc.T("Settings.BackupDone", record!.FilePath));
    }
}
