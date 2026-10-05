using System.IO;
using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClothingStore.Desktop.ViewModels;

public sealed partial class UsersViewModel(IDialogService dialogs, UserService users, Session session) : ViewModelBase(dialogs), IPageViewModel
{
    public string Title => "Users";

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
            Dialogs.Error("Could not load users.", ex);
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
            if (SelectedUser.Id == session.User.Id) Dialogs.Info("Changes to your own account apply the next time you sign in.");
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

    public override string Title => Id == 0 ? "New user" : $"Edit user — {Username}";
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

public sealed partial class SettingsViewModel(IDialogService dialogs, SettingsService settings, BackupService backup)
    : ViewModelBase(dialogs), IPageViewModel
{
    public string Title => "Settings";
    public string DatabaseName => App.DatabaseName;

    [ObservableProperty]
    public partial StoreSettings Settings { get; set; } = new();

    public async Task OnNavigatedToAsync() => await ReloadAsync();

    [RelayCommand]
    private Task ReloadAsync() => RunAsync(async () => Settings = await settings.GetAsync());

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        await settings.SaveAsync(Settings);
        Settings = await settings.GetAsync();
        Dialogs.Info("Settings saved.");
    });

    [RelayCommand]
    private async Task BackupAsync()
    {
        string? path = null;
        if (await RunAsync(async () => path = await backup.BackupAsync(settings.Current.BackupFolder)))
            Dialogs.Info($"Backup written and verified on the database server:\n{path}\n\n" +
                         "To restore it, use Restore Database in SQL Server Management Studio.");
    }
}
