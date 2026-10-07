using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Data.Seeding;

public class DatabaseInitializer(IDbContextFactory<PosDbContext> factory)
{
    public const string DefaultAdminUser = "admin";
    public const string DefaultAdminPassword = "admin123";

    /// <summary>
    /// Creates the database if needed, applies migrations and makes sure the store has settings and an administrator.
    /// </summary>
    public async Task InitializeAsync(bool seedDemoData, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
        await TuneDatabaseAsync(db, ct);

        if (!await db.Settings.AnyAsync(ct)) db.Settings.Add(new StoreSettings());

        if (!await db.Users.AnyAsync(ct))
        {
            db.Users.Add(new User
            {
                Username = DefaultAdminUser,
                FullName = "Administrator",
                Role = UserRole.Admin,
                PasswordHash = PasswordHasher.Hash(DefaultAdminPassword),
                MustChangePassword = true,
            });
        }
        await db.SaveChangesAsync(ct);

        if (seedDemoData && !await db.Products.AnyAsync(ct))
            await DemoDataSeeder.SeedAsync(db, ct);
    }

    /// <summary>
    /// True when the database exists and this version still has to upgrade it (so a backup should be taken first).
    /// When in doubt, true.
    /// </summary>
    public async Task<bool> NeedsUpgradeAsync(CancellationToken ct = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            if (!await db.Database.CanConnectAsync(ct)) return false; // no database yet: nothing to protect
            return (await db.Database.GetPendingMigrationsAsync(ct)).Any();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return true;
        }
    }

    /// <summary>
    /// SQL Server Express creates databases with AUTO_CLOSE on: the database shuts down whenever no connection is
    /// open and every screen opened after an idle moment waits seconds for it to start again (and its query plans
    /// are thrown away). AUTO_SHRINK causes similar stalls. Both are switched off; without permission it is skipped.
    /// </summary>
    private static async Task TuneDatabaseAsync(PosDbContext db, CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                IF CAST(DATABASEPROPERTYEX(DB_NAME(), 'IsAutoClose') AS int) = 1 ALTER DATABASE CURRENT SET AUTO_CLOSE OFF;
                IF CAST(DATABASEPROPERTYEX(DB_NAME(), 'IsAutoShrink') AS int) = 1 ALTER DATABASE CURRENT SET AUTO_SHRINK OFF;
                """, ct);
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            // The login may not own the database; the app still works, just slower after idle periods.
        }
    }
}
