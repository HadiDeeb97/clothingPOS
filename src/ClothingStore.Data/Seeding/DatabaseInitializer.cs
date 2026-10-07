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
        // An existing database is tuned first, so it doesn't shut itself down (AUTO_CLOSE) while being upgraded.
        var existed = await db.Database.CanConnectAsync(ct);
        if (existed) await TuneDatabaseAsync(db, ct);
        await MigrateAsync(db, ct);
        if (!existed) await TuneDatabaseAsync(db, ct);

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
    /// Applies pending migrations. EF Core holds a lock on the database while upgrading and releases it at the end; on
    /// some SQL Server Express setups that release fails ("Cannot release the application lock ... __EFMigrationsLock
    /// because it is not currently held") after the upgrade itself has finished, which stopped the first start after
    /// every update. When that happens the upgrade is checked: done means carry on, anything left is applied again.
    /// </summary>
    private static async Task MigrateAsync(PosDbContext db, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(ct);
                return;
            }
            catch (Exception ex) when (IsMigrationLockReleaseError(ex))
            {
                await db.Database.CloseConnectionAsync();
                if (!(await db.Database.GetPendingMigrationsAsync(ct)).Any()) return; // upgraded; only the unlock failed
                if (attempt >= 2) throw;
            }
        }
    }

    /// <summary>SQL Server error 1223 for EF Core's migration lock: the lock was already gone when EF released it.</summary>
    internal static bool IsMigrationLockReleaseError(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is Microsoft.Data.SqlClient.SqlException sql && (sql.Number == 1223 || sql.Message.Contains("__EFMigrationsLock", StringComparison.Ordinal)))
                return true;
        return false;
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
