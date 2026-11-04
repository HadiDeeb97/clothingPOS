using ClothingStore.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Data.Services;

/// <summary>Each till's own settings, kept in the shop database (see <see cref="TillSettings"/>).</summary>
public class TillSettingsService(IDbContextFactory<PosDbContext> factory)
{
    /// <summary>This till's saved settings (JSON), or null when it hasn't saved any yet.</summary>
    public async Task<string?> GetAsync(string tillId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.TillSettings.AsNoTracking().Where(t => t.TillId == tillId).Select(t => t.Json).FirstOrDefaultAsync(ct);
    }

    /// <summary>Keeps this till's settings, replacing what it saved before.</summary>
    public async Task SaveAsync(string tillId, string? tillName, string json, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tillId);
        for (var attempt = 1; ; attempt++)
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var row = await db.TillSettings.FirstOrDefaultAsync(t => t.TillId == tillId, ct);
            if (row is null) db.TillSettings.Add(row = new TillSettings { TillId = tillId });
            row.TillName = QueryHelpers.Clean(tillName);
            row.Json = json;
            row.UpdatedAt = DateTime.Now;
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (attempt < 3 && QueryHelpers.IsUniqueViolation(ex))
            {
                // Saved for the first time from two places at once: the row exists now, so update it.
            }
        }
    }
}
