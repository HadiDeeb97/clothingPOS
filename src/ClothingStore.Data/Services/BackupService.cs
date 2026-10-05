using ClothingStore.Core;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Data.Services;

public class BackupService(IDbContextFactory<PosDbContext> factory)
{
    private static readonly TimeSpan BackupTimeout = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Takes a full copy-only backup on the SQL Server and verifies it. The backup is safe while tills are selling.
    /// <paramref name="folder"/> is a folder on the machine that runs SQL Server (empty = the server's default
    /// backup folder) and must be writable by the SQL Server service account.
    /// </summary>
    /// <returns>Full path of the backup file on the server.</returns>
    public async Task<string> BackupAsync(string? folder, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Database.SetCommandTimeout(BackupTimeout);

        var databaseName = db.Database.GetDbConnection().Database;
        var target = await ResolveFolderAsync(db, folder, ct);
        var path = CombineServerPath(target, $"{databaseName}_{DateTime.Now:yyyyMMdd-HHmmss}.bak");

        await RunBackupAsync(db, path, ct);
        return path;
    }

    private static async Task<string> ResolveFolderAsync(PosDbContext db, string? folder, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(folder)) return folder.Trim();

        var serverDefault = await db.Database
            .SqlQueryRaw<string?>("SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000)) AS [Value]")
            .SingleAsync(ct);
        return string.IsNullOrWhiteSpace(serverDefault)
            ? throw new BusinessRuleException("SQL Server did not report a default backup folder. Set a backup folder in Settings.")
            : serverDefault;
    }

    private static async Task RunBackupAsync(PosDbContext db, string path, CancellationToken ct)
    {
        try
        {
            // COPY_ONLY leaves any backup schedule the server's administrator runs undisturbed.
            await db.Database.ExecuteSqlRawAsync(
                """
                DECLARE @db sysname = DB_NAME();
                BACKUP DATABASE @db TO DISK = @path WITH COPY_ONLY, INIT, FORMAT, CHECKSUM, NAME = @name;
                RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM;
                """,
                [new SqlParameter("@path", path), new SqlParameter("@name", "Clothing Store POS backup")],
                ct);
        }
        catch (SqlException ex)
        {
            throw new BusinessRuleException($"The backup to {path} failed: {ex.Message}");
        }
    }

    /// <summary>Joins a folder and file name using the server's path style (Windows or Linux).</summary>
    internal static string CombineServerPath(string folder, string fileName)
    {
        var separator = folder.Contains('/') && !folder.Contains('\\') ? '/' : '\\';
        return folder.TrimEnd('/', '\\') + separator + fileName;
    }
}
