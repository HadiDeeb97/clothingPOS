using System.Globalization;
using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Security;
using ClothingStore.Data.Services;
using ClothingStore.Desktop.Converters;
using ClothingStore.Desktop.Infrastructure;
using ClothingStore.Desktop.Services;
using ClothingStore.Desktop.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClothingStore.Desktop.ViewModels;

/// <summary>Cash drawer: open with a float, pay-ins/outs, X report, count and close (Z report).</summary>
public sealed partial class ShiftViewModel(
    IDialogService dialogs, ShiftService shifts, SettingsService settings, Session session, PrintService print,
    BackupService backups)
    : ViewModelBase(dialogs), IPageViewModel
{
    public string Title => "Cash Drawer";
    public Session Session => session;

    [ObservableProperty]
    public partial string OpeningFloatText { get; set; } = "100.00";

    [ObservableProperty]
    public partial ShiftSummary? Summary { get; set; }

    [ObservableProperty]
    public partial CashMovementType MovementType { get; set; } = CashMovementType.PayOut;

    [ObservableProperty]
    public partial string MovementAmountText { get; set; } = "";

    [ObservableProperty]
    public partial string? MovementReason { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VariancePreview))]
    public partial string CountedCashText { get; set; } = "";

    [ObservableProperty]
    public partial string? CloseNotes { get; set; }

    [ObservableProperty]
    public partial List<Shift> History { get; set; } = [];

    [ObservableProperty]
    public partial Shift? SelectedHistory { get; set; }

    public string VariancePreview => Summary is not null && TryParse(CountedCashText, out var counted)
        ? $"Over / (short): {CurrencyFormat.Format(counted - Summary.ExpectedCash)}"
        : "";

    public async Task OnNavigatedToAsync() => await RefreshAsync();

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        session.CurrentShift = await shifts.GetOpenShiftAsync(session.User.Id);
        Summary = session.CurrentShift is { } shift ? await shifts.GetSummaryAsync(shift.Id) : null;
        OnPropertyChanged(nameof(VariancePreview));
        var userFilter = session.Can(Permission.ViewAllShifts) ? (int?)null : session.User.Id;
        History = await shifts.GetShiftsAsync(DateTime.Today.AddDays(-60), DateTime.Today.AddDays(1), userFilter);
    });

    [RelayCommand]
    private async Task OpenShiftAsync()
    {
        if (!TryParse(OpeningFloatText, out var amount))
        {
            Dialogs.Warning("Enter the opening float (cash in the drawer).");
            return;
        }
        if (await RunAsync(() => shifts.OpenShiftAsync(session.User.Id, amount)))
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task AddMovementAsync()
    {
        if (session.CurrentShift is not { } shift) return;
        if (!TryParse(MovementAmountText, out var amount))
        {
            Dialogs.Warning("Enter an amount.");
            return;
        }
        if (await RunAsync(() => shifts.AddCashMovementAsync(shift.Id, MovementType, amount, MovementReason ?? "", session.User.Id)))
        {
            MovementAmountText = "";
            MovementReason = null;
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private void PrintXReport()
    {
        if (Summary is null) return;
        Dialogs.ShowDialog(new TextPreviewViewModel(Dialogs, print, "X report", ReceiptBuilder.ShiftReport(Summary, settings.Current)));
    }

    [RelayCommand]
    private async Task CloseShiftAsync()
    {
        if (session.CurrentShift is not { } shift || Summary is null) return;
        if (!TryParse(CountedCashText, out var counted))
        {
            Dialogs.Warning("Count the cash in the drawer and enter the total.");
            return;
        }

        var variance = counted - Summary.ExpectedCash;
        var message = $"Expected {CurrencyFormat.Format(Summary.ExpectedCash)}, counted {CurrencyFormat.Format(counted)}." +
                      (variance == 0 ? "\nThe drawer balances." : $"\nThe drawer is {(variance > 0 ? "over" : "short")} by {CurrencyFormat.Format(Math.Abs(variance))}.") +
                      "\n\nClose the shift now?";
        if (!Dialogs.Confirm(message, "Close shift")) return;

        ShiftSummary? closed = null;
        if (!await RunAsync(async () => closed = await shifts.CloseShiftAsync(shift.Id, counted, CloseNotes))) return;

        session.CurrentShift = null;
        CountedCashText = "";
        CloseNotes = null;
        Dialogs.ShowDialog(new TextPreviewViewModel(Dialogs, print, "Z report", ReceiptBuilder.ShiftReport(closed!, settings.Current)));
        await BackUpAfterCloseAsync();
        await RefreshAsync();
    }

    private async Task BackUpAfterCloseAsync()
    {
        try
        {
            IsBusy = true;
            await backups.BackupAfterShiftCloseAsync(session.User.Id);
        }
        catch (Exception ex)
        {
            Dialogs.Warning($"The shift is closed, but the end-of-day backup failed:\n\n{ex.GetBaseException().Message}\n\n" +
                            "Please tell your manager.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ViewHistoryReportAsync()
    {
        if (SelectedHistory is null) return;
        ShiftSummary? summary = null;
        if (await RunAsync(async () => summary = await shifts.GetSummaryAsync(SelectedHistory.Id)))
            Dialogs.ShowDialog(new TextPreviewViewModel(Dialogs, print, $"Shift #{SelectedHistory.Id}", ReceiptBuilder.ShiftReport(summary!, settings.Current)));
    }

    private static bool TryParse(string? text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value) ||
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}
