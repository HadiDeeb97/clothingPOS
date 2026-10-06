using System.Data;
using System.Globalization;
using ClothingStore.Core;
using ClothingStore.Core.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ClothingStore.Core.Localization;

namespace ClothingStore.Data.Services;

/// <summary>
/// Full copy-only backups taken by SQL Server, checksummed and verified, and logged in <see cref="BackupRecord"/>.
/// Backups are safe while tills are selling.
/// <para>
/// Manual backups get a timestamped file. Automatic ones (scheduled and after a shift closes) reuse one file per
/// weekday, so the last seven days are kept without the app having to delete files on the server.
/// </para>
/// </summary>
public class BackupService(IDbContextFactory<PosDbContext> factory)
{
    private static readonly TimeSpan BackupTimeout = TimeSpan.FromMinutes(30);

    /// <summary>After a failed automatic backup, wait this long before trying again.</summary>
    public static readonly TimeSpan RetryAfterFailure = TimeSpan.FromHours(1);

    /// <summary>Held while a backup runs so two tills never back up at the same time.</summary>
    private const string LockResource = "ClothingStorePOS.Backup";

    /// <summary>
    /// Backs up now to the folder set in Settings. Throws <see cref="BusinessRuleException"/> if the backup fails
    /// (the failure is still logged) or another till is backing up.
    /// </summary>
    public async Task<BackupRecord> BackupAsync(BackupKind kind, int? userId, CancellationToken ct = default) =>
        await TryBackupAsync(kind, userId, ct)
        ?? throw new BusinessRuleException(Loc.T("Err.BackupBusy"));

    /// <summary>
    /// Runs the scheduled backup if automatic backups are on and the last successful backup is older than the
    /// configured interval. Returns null when nothing was due.
    /// </summary>
    public async Task<BackupRecord?> RunIfDueAsync(CancellationToken ct = default)
    {
        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var settings = await GetSettingsAsync(db, ct);
            if (!settings.AutoBackupEnabled) return null;

            var now = DateTime.Now;
            var lastSuccess = await db.BackupRecords.Where(b => b.Succeeded).MaxAsync(b => (DateTime?)b.StartedAt, ct);
            if (lastSuccess is not null && now - lastSuccess < TimeSpan.FromHours(settings.AutoBackupIntervalHours)) return null;

            var lastAttempt = await db.BackupRecords.AsNoTracking()
                .Where(b => b.Kind != BackupKind.Manual)
                .OrderByDescending(b => b.StartedAt)
                .FirstOrDefaultAsync(ct);
            if (lastAttempt is { Succeeded: false } && now - lastAttempt.StartedAt < RetryAfterFailure) return null;
        }

        return await TryBackupAsync(BackupKind.Scheduled, null, ct);
    }

    /// <summary>End-of-day backup after a shift is closed, if automatic backups are on. Returns null when skipped.</summary>
    public async Task<BackupRecord?> BackupAfterShiftCloseAsync(int userId, CancellationToken ct = default)
    {
        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            if (!(await GetSettingsAsync(db, ct)).AutoBackupEnabled) return null;
        }
        return await TryBackupAsync(BackupKind.ShiftClose, userId, ct);
    }

    public async Task<List<BackupRecord>> GetRecentAsync(int count = 10, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.BackupRecords.AsNoTracking().OrderByDescending(b => b.StartedAt).Take(count).ToListAsync(ct);
    }

    /// <summary>Returns null if another till holds the backup lock.</summary>
    private async Task<BackupRecord?> TryBackupAsync(BackupKind kind, int? userId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Database.SetCommandTimeout(BackupTimeout);
        var settings = await GetSettingsAsync(db, ct);

        // The lock is owned by the session, so everything below must run on this one connection.
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            if (!await TryLockAsync(db, ct)) return null;

            var record = new BackupRecord { Kind = kind, UserId = userId, StartedAt = DateTime.Now };
            try
            {
                try
                {
                    var folder = await ResolveFolderAsync(db, settings.BackupFolder, ct);
                    record.FilePath = CombineServerPath(folder, FileName(db.Database.GetDbConnection().Database, kind, record.StartedAt));
                    await RunBackupAsync(db, record.FilePath, ct);
                    record.Succeeded = true;
                }
                catch (Exception ex) when (ex is SqlException or BusinessRuleException)
                {
                    record.Error = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                }
                finally
                {
                    record.CompletedAt = DateTime.Now;
                    db.BackupRecords.Add(record);
                    await db.SaveChangesAsync(CancellationToken.None);
                }
            }
            finally
            {
                await ReleaseLockAsync(db);
            }

            return record.Succeeded
                ? record
                : throw new BusinessRuleException(record.FilePath is null
                    ? record.Error!
                    : Loc.T("Err.BackupFailed", record.FilePath, record.Error));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static string FileName(string databaseName, BackupKind kind, DateTime at) => kind == BackupKind.Manual
        ? $"{databaseName}_{at:yyyyMMdd-HHmmss}.bak"
        : $"{databaseName}_auto_{at.ToString("ddd", CultureInfo.InvariantCulture)}.bak";

    private static async Task<StoreSettings> GetSettingsAsync(PosDbContext db, CancellationToken ct) =>
        await db.Settings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct) ?? new StoreSettings();

    private static async Task<bool> TryLockAsync(PosDbContext db, CancellationToken ct)
    {
        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        await db.Database.ExecuteSqlRawAsync(
            """
            DECLARE @r int;
            EXEC @r = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0;
            SET @result = @r;
            """,
            [new SqlParameter("@resource", LockResource), result],
            ct);
        return (int)result.Value >= 0;
    }

    private static async Task ReleaseLockAsync(PosDbContext db) =>
        await db.Database.ExecuteSqlRawAsync(
            "EXEC sp_releaseapplock @Resource = @resource, @LockOwner = 'Session';",
            [new SqlParameter("@resource", LockResource)]);

    private static async Task<string> ResolveFolderAsync(PosDbContext db, string? folder, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(folder)) return folder.Trim();

        var serverDefault = (await db.Database
            .SqlQueryRaw<string?>("SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000)) AS [Value]")
            .ToListAsync(ct)).SingleOrDefault();
        return string.IsNullOrWhiteSpace(serverDefault)
            ? throw new BusinessRuleException(Loc.T("Err.NoDefaultBackupFolder"))
            : serverDefault;
    }

    private static Task RunBackupAsync(PosDbContext db, string path, CancellationToken ct) =>
        // COPY_ONLY leaves any backup schedule the server's administrator runs undisturbed.
        db.Database.ExecuteSqlRawAsync(
            """
            DECLARE @db sysname = DB_NAME();
            BACKUP DATABASE @db TO DISK = @path WITH COPY_ONLY, INIT, FORMAT, CHECKSUM, NAME = @name;
            RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM;
            """,
            [new SqlParameter("@path", path), new SqlParameter("@name", "Clothing Store POS backup")],
            ct);

    /// <summary>Joins a folder and file name using the server's path style (Windows or Linux).</summary>
    internal static string CombineServerPath(string folder, string fileName)
    {
        var separator = folder.Contains('/') && !folder.Contains('\\') ? '/' : '\\';
        return folder.TrimEnd('/', '\\') + separator + fileName;
    }
}
