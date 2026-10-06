using ClothingStore.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Data.Services;

/// <summary>Dates from the database that show how far time has really moved on (so a PC's clock can't be set back).</summary>
public sealed record ClockEvidence(DateTime? ServerNow, DateTime? LatestActivity, string? LastSeenRecord);

public class LicenseClockService(IDbContextFactory<PosDbContext> factory)
{
    private const string LastSeenKey = "license.lastSeen";

    public async Task<ClockEvidence> GetEvidenceAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        DateTime? serverNow = null;
        try
        {
            serverNow = await db.Database.SqlQueryRaw<DateTime>("SELECT SYSDATETIME() AS [Value]").SingleAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not fatal: the other evidence still counts.
        }

        var latestSale = await db.Sales.MaxAsync(s => (DateTime?)s.CreatedAt, ct);
        var latestReturn = await db.Returns.MaxAsync(r => (DateTime?)r.CreatedAt, ct);
        var latestShift = await db.Shifts.MaxAsync(s => (DateTime?)s.OpenedAt, ct);
        var latest = new[] { latestSale, latestReturn, latestShift }.Where(d => d is not null).DefaultIfEmpty(null).Max();

        var record = await db.AppState.AsNoTracking().Where(s => s.Key == LastSeenKey).Select(s => s.Value).FirstOrDefaultAsync(ct);
        return new ClockEvidence(serverNow, latest, record);
    }

    /// <summary>Saves the (signed) last-seen record shared by every till.</summary>
    public async Task SaveLastSeenAsync(string record, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.AppState.FirstOrDefaultAsync(s => s.Key == LastSeenKey, ct);
        if (row is null) db.AppState.Add(new AppState { Key = LastSeenKey, Value = record });
        else row.Value = record;
        await db.SaveChangesAsync(ct);
    }
}
