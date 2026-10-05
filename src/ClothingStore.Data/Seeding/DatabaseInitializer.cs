using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Data.Seeding;

public class DatabaseInitializer(IDbContextFactory<PosDbContext> factory)
{
    public const string DefaultAdminUser = "admin";
    public const string DefaultAdminPassword = "admin123";

    /// <summary>Applies migrations and makes sure the store has settings and an administrator.</summary>
    public async Task InitializeAsync(bool seedDemoData, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);

        if (db.Database.GetDbConnection().DataSource is { Length: > 0 } source && !source.Contains(":memory:"))
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);

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
}
