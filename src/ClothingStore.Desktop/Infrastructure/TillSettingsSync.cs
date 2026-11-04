using ClothingStore.Core.Localization;
using ClothingStore.Data.Services;

namespace ClothingStore.Desktop.Infrastructure;

/// <summary>
/// Keeps this till's settings (printers, cash drawer, customer screen, text size, label layout, panel and column sizes)
/// in the shop database as well as on the PC. At start the database copy wins, so settings the PC lost (a restart in
/// the middle of a save, a reinstall, another Windows account on the same till) come back; changes made while the
/// database couldn't be reached are sent first instead. After that, every change is sent a moment later.
/// </summary>
public sealed class TillSettingsSync(TillSettingsService service, Session session)
{
    /// <summary>Changes in a row (dragging a panel) are sent once, this long after the last one.</summary>
    private static readonly TimeSpan SendAfter = TimeSpan.FromSeconds(2);

    private CancellationTokenSource? _waiting;
    private bool _listening;

    /// <summary>Once the database is ready at start: brings back or sends this till's settings, then follows every change.</summary>
    public async Task StartAsync()
    {
        var prefs = LocalPreferences.Current;
        try
        {
            if (!prefs.TillSyncPending && await service.GetAsync(session.TillId) is { } kept && prefs.ApplyTillJson(kept))
            {
                prefs.Save(notify: false);
                // What was put back before anything is shown: the language of the sign-in screen and the font.
                Loc.SetLanguage(prefs.Language);
                UiFont.Apply();
            }
            else
            {
                await SendAsync(); // first start with this version, or changes not sent yet
            }
        }
        catch (Exception)
        {
            // Database busy: this PC's own copy is used; it is sent with the next change.
        }

        if (_listening) return;
        _listening = true;
        LocalPreferences.KeepsTillCopy = true;
        LocalPreferences.Saved += (_, _) => SendSoon();
    }

    private void SendSoon()
    {
        _waiting?.Cancel();
        var waiting = _waiting = new CancellationTokenSource();
        _ = SendLaterAsync(waiting.Token);
    }

    private async Task SendLaterAsync(CancellationToken ct)
    {
        try { await Task.Delay(SendAfter, ct); }
        catch (OperationCanceledException) { return; } // a newer change will be sent instead
        await SendAsync();
    }

    private async Task SendAsync()
    {
        var prefs = LocalPreferences.Current;
        try
        {
            await service.SaveAsync(session.TillId, session.TillName, prefs.ToTillJson());
            if (prefs.TillSyncPending)
            {
                prefs.TillSyncPending = false;
                prefs.Save(notify: false);
            }
        }
        catch (Exception)
        {
            // The database couldn't be reached: kept on the PC (marked to send) and sent with the next change or start.
            if (!prefs.TillSyncPending)
            {
                prefs.TillSyncPending = true;
                prefs.Save(notify: false);
            }
        }
    }
}
