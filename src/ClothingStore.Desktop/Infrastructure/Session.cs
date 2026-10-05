using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Security;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClothingStore.Desktop.Infrastructure;

/// <summary>The signed-in user and their open cash-drawer shift.</summary>
public sealed partial class Session : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn), nameof(UserDisplay))]
    public partial User? CurrentUser { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOpenShift), nameof(ShiftDisplay))]
    public partial Shift? CurrentShift { get; set; }

    public bool IsSignedIn => CurrentUser is not null;
    public bool HasOpenShift => CurrentShift is { Status: ShiftStatus.Open };

    public User User => CurrentUser ?? throw new InvalidOperationException("No user is signed in.");

    public string UserDisplay => CurrentUser is null ? "" : $"{CurrentUser.FullName} · {CurrentUser.Role}";

    public string ShiftDisplay => CurrentShift is null
        ? "No open shift"
        : $"Shift open since {CurrentShift.OpenedAt:HH:mm}";

    public bool Can(Permission permission) => CurrentUser is not null && Permissions.Has(CurrentUser.Role, permission);

    public void SignOut()
    {
        CurrentShift = null;
        CurrentUser = null;
    }
}
