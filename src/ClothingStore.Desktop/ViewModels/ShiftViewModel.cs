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
using ClothingStore.Core.Localization;

namespace ClothingStore.Desktop.ViewModels;

/// <summary>Cash drawer: open with a float, pay-ins/outs, X report, count and close (Z report).</summary>
public sealed partial class ShiftViewModel(
    IDialogService dialogs, ShiftService shifts, SettingsService settings, Session session, PrintService print,
    BackupService backups)
    : ViewModelBase(dialogs), IPageViewModel
{
    public string Title => Loc.T("Nav.CashDrawer");
    public Session Session => session;

    [ObservableProperty]
    public partial string OpeningFloatText { get; set; } = "100.00";

    [ObservableProperty]
    public partial string OpeningFloatLbpText { get; set; } = "0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLbp))]
    public partial ShiftSummary? Summary { get; set; }

    /// <summary>Lebanese pounds are counted separately from dollars.</summary>
    public bool ShowLbp => settings.Current.ActiveLbpRate > 0 || Summary?.ShowLbp == true;

    [ObservableProperty]
    public partial CashCurrency MovementCurrency { get; set; } = CashCurrency.Usd;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VariancePreviewLbp))]
    public partial string CountedCashLbpText { get; set; } = "";

    public string VariancePreviewLbp => Summary is not null && TryParse(CountedCashLbpText, out var counted)
        ? Loc.T("Shift.VariancePreview", CurrencyFormat.Lbp(counted - Summary.ExpectedCashLbp))
        : "";

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
        ? Loc.T("Shift.VariancePreview", CurrencyFormat.Format(counted - Summary.ExpectedCash))
        : "";

    public async Task OnNavigatedToAsync() => await RefreshAsync();

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        session.CurrentShift = await shifts.GetOpenShiftAsync(session.User.Id);
        Summary = session.CurrentShift is { } shift ? await shifts.GetSummaryAsync(shift.Id) : null;
        OnPropertyChanged(nameof(VariancePreview));
        OnPropertyChanged(nameof(VariancePreviewLbp));
        var userFilter = session.Can(Permission.ViewAllShifts) ? (int?)null : session.User.Id;
        History = await shifts.GetShiftsAsync(DateTime.Today.AddDays(-60), DateTime.Today.AddDays(1), userFilter);
    });

    [RelayCommand]
    private async Task OpenShiftAsync()
    {
        if (!TryParse(OpeningFloatText, out var amount))
        {
            Dialogs.Warning(Loc.T("Shift.EnterFloat"));
            return;
        }
        var amountLbp = 0m;
        if (ShowLbp && !string.IsNullOrWhiteSpace(OpeningFloatLbpText) && !TryParse(OpeningFloatLbpText, out amountLbp))
        {
            Dialogs.Warning(Loc.T("Shift.EnterFloat"));
            return;
        }
        if (await RunAsync(() => shifts.OpenShiftAsync(session.User.Id, amount, amountLbp)))
        {
            Dialogs.Toast(Loc.T("Shift.Opened"));
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task AddMovementAsync()
    {
        if (session.CurrentShift is not { } shift) return;
        if (!TryParse(MovementAmountText, out var amount))
        {
            Dialogs.Warning(Loc.T("Shift.EnterAmount"));
            return;
        }
        if (await RunAsync(() => shifts.AddCashMovementAsync(shift.Id, MovementType, amount, MovementReason ?? "", session.User.Id, MovementCurrency)))
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
        Dialogs.ShowDialog(new TextPreviewViewModel(Dialogs, print, Loc.T("Shift.XReport"), ReceiptBuilder.ShiftReport(Summary, settings.Current)));
    }

    [RelayCommand]
    private async Task CloseShiftAsync()
    {
        if (session.CurrentShift is not { } shift || Summary is null) return;
        if (!TryParse(CountedCashText, out var counted))
        {
            Dialogs.Warning(Loc.T("Shift.EnterCount"));
            return;
        }

        decimal? countedLbp = null;
        if (ShowLbp)
        {
            if (!TryParse(CountedCashLbpText, out var lbp))
            {
                Dialogs.Warning(Loc.T("Shift.EnterCountLbp"));
                return;
            }
            countedLbp = lbp;
        }

        var message = Loc.T("Shift.CloseConfirm",
            CurrencyFormat.Format(Summary.ExpectedCash), CurrencyFormat.Format(counted), Balance(counted - Summary.ExpectedCash, CurrencyFormat.Format));
        if (countedLbp is { } c)
            message = Loc.T("Shift.CloseConfirmBoth",
                CurrencyFormat.Format(Summary.ExpectedCash), CurrencyFormat.Format(counted), Balance(counted - Summary.ExpectedCash, CurrencyFormat.Format),
                CurrencyFormat.Lbp(Summary.ExpectedCashLbp), CurrencyFormat.Lbp(c), Balance(c - Summary.ExpectedCashLbp, CurrencyFormat.Lbp));
        if (!Dialogs.Confirm(message, Loc.T("Shift.CloseShift"))) return;

        ShiftSummary? closed = null;
        if (!await RunAsync(async () => closed = await shifts.CloseShiftAsync(shift.Id, counted, CloseNotes, countedLbp))) return;

        session.CurrentShift = null;
        CountedCashText = "";
        CountedCashLbpText = "";
        CloseNotes = null;
        Dialogs.ShowDialog(new TextPreviewViewModel(Dialogs, print, Loc.T("Shift.ZReport"), ReceiptBuilder.ShiftReport(closed!, settings.Current)));
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
            Dialogs.Warning(Loc.T("Shift.BackupFailed", ex.GetBaseException().Message));
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
            Dialogs.ShowDialog(new TextPreviewViewModel(Dialogs, print, Loc.T("Shift.ShiftNumber", SelectedHistory.Id), ReceiptBuilder.ShiftReport(summary!, settings.Current)));
    }

    private static string Balance(decimal variance, Func<decimal, string> format) =>
        variance == 0
            ? Loc.T("Shift.Balances")
            : Loc.T(variance > 0 ? "Shift.Over" : "Shift.Short", format(Math.Abs(variance)));

    private static bool TryParse(string? text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value) ||
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}
