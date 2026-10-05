using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Data.Services;

public class UserService(IDbContextFactory<PosDbContext> factory)
{
    public const int MinPasswordLength = 6;

    public async Task<User?> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) return null;

        await using var db = await factory.CreateDbContextAsync(ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username.Trim(), ct);
        if (user is null || !user.IsActive || !PasswordHasher.Verify(password, user.PasswordHash)) return null;

        user.LastLoginAt = DateTime.Now;
        await db.SaveChangesAsync(ct);
        return user;
    }

    /// <summary>Validates a manager/admin's credentials for an override (discount, return window...).</summary>
    public async Task<User?> AuthorizeAsync(string username, string password, Permission permission, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username.Trim(), ct);
        if (user is null || !user.IsActive || !PasswordHasher.Verify(password, user.PasswordHash)) return null;
        return Permissions.Has(user.Role, permission) ? user : null;
    }

    /// <summary>True until the default administrator has changed the initial password.</summary>
    public async Task<bool> IsFirstRunAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.AnyAsync(u => u.Role == UserRole.Admin && u.IsActive && u.MustChangePassword, ct);
    }

    public async Task<List<User>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync(ct);
    }

    public async Task<User> SaveAsync(User user, string? newPassword, CancellationToken ct = default)
    {
        user.Username = user.Username.Trim();
        user.FullName = user.FullName.Trim();
        if (user.Username.Length < 3) throw new BusinessRuleException("Username must be at least 3 characters.");
        if (user.Username.Any(char.IsWhiteSpace)) throw new BusinessRuleException("Username cannot contain spaces.");
        if (user.FullName.Length == 0) throw new BusinessRuleException("Full name is required.");
        if (user.Id == 0 && string.IsNullOrEmpty(newPassword)) throw new BusinessRuleException("A password is required for new users.");
        if (!string.IsNullOrEmpty(newPassword)) ValidatePassword(newPassword);

        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.Users.AnyAsync(u => u.Username == user.Username && u.Id != user.Id, ct))
            throw new BusinessRuleException($"Username '{user.Username}' is already taken.");

        User entity;
        if (user.Id == 0)
        {
            entity = new User { CreatedAt = DateTime.Now };
            db.Users.Add(entity);
        }
        else
        {
            entity = await db.Users.FirstOrDefaultAsync(u => u.Id == user.Id, ct)
                     ?? throw new BusinessRuleException("User not found.");
            var losingAdmin = entity.Role == UserRole.Admin && entity.IsActive && (user.Role != UserRole.Admin || !user.IsActive);
            if (losingAdmin && !await db.Users.AnyAsync(u => u.Id != user.Id && u.Role == UserRole.Admin && u.IsActive, ct))
                throw new BusinessRuleException("There must be at least one active administrator.");
        }

        entity.Username = user.Username;
        entity.FullName = user.FullName;
        entity.Role = user.Role;
        entity.IsActive = user.IsActive;
        if (!string.IsNullOrEmpty(newPassword))
        {
            entity.PasswordHash = PasswordHasher.Hash(newPassword);
            entity.MustChangePassword = user.MustChangePassword;
        }

        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        ValidatePassword(newPassword);
        await using var db = await factory.CreateDbContextAsync(ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new BusinessRuleException("User not found.");
        if (!PasswordHasher.Verify(currentPassword, user.PasswordHash))
            throw new BusinessRuleException("Current password is incorrect.");
        if (currentPassword == newPassword)
            throw new BusinessRuleException("The new password must be different from the current one.");

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        user.MustChangePassword = false;
        await db.SaveChangesAsync(ct);
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < MinPasswordLength)
            throw new BusinessRuleException($"Password must be at least {MinPasswordLength} characters.");
    }
}
