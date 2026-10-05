using ClothingStore.Core;
using ClothingStore.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Data.Services;

public class CustomerService(IDbContextFactory<PosDbContext> factory)
{
    public async Task<List<Customer>> SearchAsync(string? text = null, bool includeInactive = false, int max = 200, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Customers.AsNoTracking().Where(c => includeInactive || c.IsActive);
        if (!string.IsNullOrWhiteSpace(text))
        {
            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var pattern = QueryHelpers.LikePattern(word);
                query = query.Where(c =>
                    EF.Functions.Like(c.FirstName, pattern, "\\") ||
                    EF.Functions.Like(c.LastName, pattern, "\\") ||
                    EF.Functions.Like(c.Phone!, pattern, "\\") ||
                    EF.Functions.Like(c.Email!, pattern, "\\"));
            }
        }
        return await query.OrderBy(c => c.FirstName).ThenBy(c => c.LastName).Take(max).ToListAsync(ct);
    }

    public async Task<Customer?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<Customer> SaveAsync(Customer customer, CancellationToken ct = default)
    {
        customer.FirstName = customer.FirstName.Trim();
        customer.LastName = customer.LastName.Trim();
        customer.Phone = QueryHelpers.Clean(customer.Phone);
        customer.Email = QueryHelpers.Clean(customer.Email);
        if (customer.FirstName.Length == 0) throw new BusinessRuleException("First name is required.");
        if (customer.Email is not null && !customer.Email.Contains('@')) throw new BusinessRuleException("Email address looks invalid.");

        await using var db = await factory.CreateDbContextAsync(ct);
        if (customer.Phone is not null && await db.Customers.AnyAsync(c => c.Phone == customer.Phone && c.Id != customer.Id, ct))
            throw new BusinessRuleException($"Another customer already uses phone number {customer.Phone}.");

        var entity = customer.Id == 0
            ? db.Customers.Add(new Customer { CreatedAt = DateTime.Now }).Entity
            : await db.Customers.FindAsync([customer.Id], ct) ?? throw new BusinessRuleException("Customer not found.");

        // Points and store credit only change through sales, returns and explicit adjustments.
        entity.FirstName = customer.FirstName;
        entity.LastName = customer.LastName;
        entity.Phone = customer.Phone;
        entity.Email = customer.Email;
        entity.Birthday = customer.Birthday;
        entity.Notes = QueryHelpers.Clean(customer.Notes);
        entity.IsActive = customer.IsActive;

        await SaveAsync(db, ct);
        return entity;
    }

    /// <summary>Manually add (positive) or remove (negative) store credit, e.g. goodwill gestures.</summary>
    public async Task<Customer> AdjustStoreCreditAsync(int customerId, decimal amount, CancellationToken ct = default)
    {
        amount = Money.Round(amount);
        if (amount == 0) throw new BusinessRuleException("Amount cannot be zero.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var customer = await db.Customers.FindAsync([customerId], ct) ?? throw new BusinessRuleException("Customer not found.");
        if (customer.StoreCredit + amount < 0)
            throw new BusinessRuleException($"Customer only has {customer.StoreCredit:N2} store credit.");
        customer.StoreCredit += amount;
        await SaveAsync(db, ct);
        return customer;
    }

    public async Task<List<Sale>> GetPurchaseHistoryAsync(int customerId, int max = 200, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Sales.AsNoTracking()
            .Include(s => s.Lines)
            .Include(s => s.User)
            .Where(s => s.CustomerId == customerId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(max)
            .ToListAsync(ct);
    }

    private static async Task SaveAsync(PosDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("This customer was updated at another till. Please reload and try again.");
        }
    }
}
