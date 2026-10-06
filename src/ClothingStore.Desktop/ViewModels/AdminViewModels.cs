using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClothingStore.Core.Localization;

namespace ClothingStore.Desktop.ViewModels;

public sealed partial class UsersViewModel(IDialogService dialogs, UserService users, Session session) : ViewModelBase(dialogs), IPageViewModel
{
    public string Title => Loc.T("Nav.Users");

    [ObservableProperty]
    public partial List<User> Users { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    public partial User? SelectedUser { get; set; }

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
        if (Dialogs.ShowDialog(new UserEditorViewModel(Dialogs, users, null))) await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedUser is null) return;
        if (Dialogs.ShowDialog(new UserEditorViewModel(Dialogs, users, SelectedUser)))
        {
            Dialogs.Toast(SelectedUser.Id == session.User.Id ? Loc.T("Users.OwnAccountNextSignIn") : Loc.T("Common.Saved"));
            await RefreshAsync();
        }
    }

    private bool HasSelection() => SelectedUser is not null;
}

public sealed partial class UserEditorViewModel : DialogViewModelBase
{
    private readonly UserService _users;

    public UserEditorViewModel(IDialogService dialogs, UserService users, User? existing) : base(dialogs)
    {
        _users = users;
        Id = existing?.Id ?? 0;
        Username = existing?.Username ?? "";
        FullName = existing?.FullName ?? "";
        Role = existing?.Role ?? UserRole.Cashier;
        IsActive = existing?.IsActive ?? true;
        MustChangePassword = existing is null;
    }

    public override string Title => Id == 0 ? Loc.T("Users.New") : Loc.T("Users.Edit", Username);
    public int Id { get; }
    public bool IsNew => Id == 0;
    public UserRole[] Roles { get; } = Enum.GetValues<UserRole>();

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
        }, string.IsNullOrEmpty(Password) ? null : Password);
        Close(true);
    });

    [RelayCommand]
    private void Cancel() => Close(false);
}

public sealed record LanguageOption(string Code, string Name);

public sealed partial class SettingsViewModel(IDialogService dialogs, SettingsService settings, BackupService backup, Session session)
    : ViewModelBase(dialogs), IPageViewModel
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

    public async Task OnNavigatedToAsync() => await ReloadAsync();

    [RelayCommand]
    private Task ReloadAsync() => RunAsync(async () =>
    {
        Settings = await settings.GetAsync();
        Backups = await backup.GetRecentAsync();
    });

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        await settings.SaveAsync(Settings);
        Settings = await settings.GetAsync();
        Dialogs.Toast(Loc.T("Settings.Saved"));
    });

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
