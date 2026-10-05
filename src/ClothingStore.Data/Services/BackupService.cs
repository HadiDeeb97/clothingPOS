using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Data.Services;

public class BackupService(IDbContextFactory<PosDbContext> factory)
{
    /// <summary>Writes a consistent copy of the live database (safe while the app is running).</summary>
    public async Task BackupAsync(string destinationPath, CancellationToken ct = default)
    {
        var fullPath = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        if (File.Exists(fullPath)) File.Delete(fullPath);

        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.ExecuteSqlRawAsync("VACUUM INTO {0}", [fullPath], ct);
    }
}
