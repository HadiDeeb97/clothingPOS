using System.Text.Json;
using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Pricing;
using ClothingStore.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Data.Services;

public class SalesService(IDbContextFactory<PosDbContext> factory)
{
    public async Task<Sale> CompleteSaleAsync(CheckoutRequest request, CancellationToken ct = default)
    {
        if (request.Lines.Count == 0) throw new BusinessRuleException("The cart is empty.");
        if (request.Lines.Any(l => l.Quantity <= 0)) throw new BusinessRuleException("Quantities must be at least 1.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var settings = await db.Settings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct) ?? new StoreSettings();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive, ct)
                   ?? throw new BusinessRuleException("Cashier account not found or inactive.");

        Shift? shift = null;
        if (request.ShiftId is { } shiftId)
        {
            shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId, ct);
            if (shift is null || shift.Status != ShiftStatus.Open)
                throw new BusinessRuleException("The cash drawer shift is closed. Open a shift before selling.");
        }

        Customer? customer = null;
        if (request.CustomerId is { } customerId)
            customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct)
                       ?? throw new BusinessRuleException("Customer not found.");

        // Load variants and price the cart with server-side prices.
        var variantIds = request.Lines.Select(l => l.VariantId).Distinct().ToList();
        var variants = await db.ProductVariants
            .Include(v => v.Product).ThenInclude(p => p!.Category)
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);

        foreach (var line in request.Lines)
        {
            if (!variants.TryGetValue(line.VariantId, out var v) || !v.IsActive || !v.Product!.IsActive)
                throw new BusinessRuleException("One of the items is no longer available for sale.");
        }

        var totals = CartCalculator.Calculate(
            request.Lines.Select(l => new CartLineInput(variants[l.VariantId].EffectivePrice, l.Quantity, l.DiscountType, l.DiscountValue)).ToList(),
            request.CartDiscountType, request.CartDiscountValue, settings.TaxRate, settings.PricesIncludeTax);

        await EnforceDiscountLimitAsync(db, user, request, totals, settings, ct);

        // Payments.
        var sale = new Sale
        {
            CreatedAt = DateTime.Now,
            UserId = user.Id,
            CustomerId = customer?.Id,
            ShiftId = shift?.Id,
            Subtotal = totals.Subtotal,
            DiscountTotal = totals.DiscountTotal,
            TaxTotal = totals.TaxTotal,
            Total = totals.Total,
            CartDiscountType = request.CartDiscountType,
            CartDiscountValue = request.CartDiscountType == DiscountType.None ? 0 : request.CartDiscountValue,
            Notes = QueryHelpers.Clean(request.Notes),
            Status = SaleStatus.Completed,
        };
        ApplyPayments(sale, request.Payments, customer, settings);

        // Lines + stock.
        sale.ReceiptNumber = await NextReceiptNumberAsync(db, settings.ReceiptPrefix, sale.CreatedAt, ct);
        for (var i = 0; i < request.Lines.Count; i++)
        {
            var req = request.Lines[i];
            var v = variants[req.VariantId];
            var r = totals.Lines[i];
            sale.Lines.Add(new SaleLine
            {
                ProductVariantId = v.Id,
                ProductName = v.Product!.Name,
                VariantDescription = v.Description,
                Sku = v.Sku,
                CategoryName = v.Product.Category?.Name,
                UnitPrice = v.EffectivePrice,
                UnitCost = v.EffectiveCost,
                Quantity = req.Quantity,
                DiscountAmount = r.Discount,
                TaxAmount = r.Tax,
                LineTotal = r.Total,
            });
            StockLedger.Apply(db, v, -req.Quantity, StockMovementType.Sale, user.Id, sale.ReceiptNumber,
                allowNegative: settings.AllowNegativeStock);
        }

        db.Sales.Add(sale);
        await SaveAsync(db, ct);
        return (await GetAsync(sale.Id, ct))!;
    }

    private static async Task EnforceDiscountLimitAsync(PosDbContext db, User user, CheckoutRequest request, CartTotals totals, StoreSettings settings, CancellationToken ct)
    {
        if (Permissions.Has(user.Role, Permission.OverrideDiscountLimit)) return;

        var worstLine = totals.Lines.Select(l => CartCalculator.DiscountPercent(l.Gross, l.Discount)).DefaultIfEmpty(0).Max();
        var overall = CartCalculator.DiscountPercent(totals.Subtotal, totals.DiscountTotal);
        if (Math.Max(worstLine, overall) <= settings.MaxCashierDiscountPercent) return;

        if (request.ApprovedByUserId is { } approverId)
        {
            var approver = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == approverId && u.IsActive, ct);
            if (approver is not null && Permissions.Has(approver.Role, Permission.OverrideDiscountLimit)) return;
        }

        throw new BusinessRuleException(
            $"Discounts above {settings.MaxCashierDiscountPercent:0.##}% need manager approval.");
    }

    internal static void ApplyPayments(Sale sale, IReadOnlyList<PaymentInput> payments, Customer? customer, StoreSettings settings)
    {
        if (payments.Any(p => p.Amount < 0)) throw new BusinessRuleException("Payment amounts cannot be negative.");

        var nonCash = payments.Where(p => p.Method != PaymentMethod.Cash && p.Amount > 0).ToList();
        var nonCashTotal = nonCash.Sum(p => Money.Round(p.Amount));
        if (nonCashTotal > sale.Total)
            throw new BusinessRuleException("Card, wallet and credit payments cannot exceed the total. Only cash can give change.");

        var cashTendered = Money.Round(payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount));
        var cashDue = sale.Total - nonCashTotal;
        if (cashTendered < cashDue)
            throw new BusinessRuleException($"Payment is short by {cashDue - cashTendered:N2}.");

        foreach (var p in nonCash)
        {
            var amount = Money.Round(p.Amount);
            switch (p.Method)
            {
                case PaymentMethod.StoreCredit:
                    if (customer is null) throw new BusinessRuleException("Select a customer to pay with store credit.");
                    if (customer.StoreCredit < amount)
                        throw new BusinessRuleException($"Customer only has {customer.StoreCredit:N2} store credit.");
                    customer.StoreCredit -= amount;
                    break;

                case PaymentMethod.LoyaltyPoints:
                    if (customer is null) throw new BusinessRuleException("Select a customer to redeem loyalty points.");
                    if (settings.LoyaltyPointValue <= 0) throw new BusinessRuleException("Loyalty redemption is disabled.");
                    var points = (int)Math.Ceiling(amount / settings.LoyaltyPointValue);
                    if (customer.LoyaltyPoints < points)
                        throw new BusinessRuleException($"Customer only has {customer.LoyaltyPoints} points.");
                    customer.LoyaltyPoints -= points;
                    sale.LoyaltyPointsRedeemed += points;
                    break;
            }
            sale.Payments.Add(new Payment { Method = p.Method, Amount = amount, Reference = QueryHelpers.Clean(p.Reference) });
        }

        if (cashDue > 0)
            sale.Payments.Add(new Payment { Method = PaymentMethod.Cash, Amount = cashDue });

        sale.CashTendered = cashTendered;
        sale.ChangeGiven = cashTendered - Math.Max(cashDue, 0);

        if (customer is not null)
        {
            var earningSpend = sale.Payments
                .Where(p => p.Method is PaymentMethod.Cash or PaymentMethod.Card or PaymentMethod.MobileWallet)
                .Sum(p => p.Amount);
            sale.LoyaltyPointsEarned = (int)Math.Floor(earningSpend * settings.LoyaltyPointsPerUnit);
            customer.LoyaltyPoints += sale.LoyaltyPointsEarned;
        }
    }

    private static async Task<string> NextReceiptNumberAsync(PosDbContext db, string prefix, DateTime date, CancellationToken ct)
    {
        var stem = QueryHelpers.DayStem(prefix, date);
        var last = await db.Sales
            .Where(s => s.ReceiptNumber.StartsWith(stem))
            .OrderByDescending(s => s.ReceiptNumber)
            .Select(s => s.ReceiptNumber)
            .FirstOrDefaultAsync(ct);
        return QueryHelpers.NextNumber(prefix, date, last);
    }

    public async Task<Sale?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await SaleQuery(db).FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Sale?> GetByReceiptAsync(string receiptNumber, CancellationToken ct = default)
    {
        var number = receiptNumber.Trim().ToUpperInvariant();
        await using var db = await factory.CreateDbContextAsync(ct);
        return await SaleQuery(db).FirstOrDefaultAsync(s => s.ReceiptNumber == number, ct);
    }

    public async Task<List<Sale>> SearchAsync(DateTime from, DateTime to, string? text = null, bool includeVoided = true, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Sales.AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Customer)
            .Include(s => s.Lines)
            .Include(s => s.Payments)
            .AsSplitQuery()
            .Where(s => s.CreatedAt >= from && s.CreatedAt < to);

        if (!includeVoided) query = query.Where(s => s.Status == SaleStatus.Completed);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var pattern = QueryHelpers.LikePattern(text);
            query = query.Where(s =>
                EF.Functions.Like(s.ReceiptNumber, pattern, "\\") ||
                EF.Functions.Like(s.Customer!.FirstName, pattern, "\\") ||
                EF.Functions.Like(s.Customer!.LastName, pattern, "\\") ||
                EF.Functions.Like(s.Customer!.Phone!, pattern, "\\") ||
                s.Lines.Any(l => EF.Functions.Like(l.ProductName, pattern, "\\") || EF.Functions.Like(l.Sku, pattern, "\\")));
        }

        return await query.OrderByDescending(s => s.CreatedAt).Take(1000).ToListAsync(ct);
    }

    /// <summary>Voids a sale that has no returns: restocks items and reverses credit/points.</summary>
    public async Task VoidSaleAsync(int saleId, int userId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleException("A reason is required to void a sale.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !Permissions.Has(user.Role, Permission.VoidSales))
            throw new BusinessRuleException("Only managers can void sales.");

        var sale = await db.Sales
            .Include(s => s.Lines).ThenInclude(l => l.ProductVariant)
            .Include(s => s.Payments)
            .Include(s => s.Customer)
            .Include(s => s.Shift)
            .FirstOrDefaultAsync(s => s.Id == saleId, ct)
            ?? throw new BusinessRuleException("Sale not found.");

        if (sale.Status == SaleStatus.Voided) throw new BusinessRuleException("This sale is already voided.");
        if (sale.Lines.Any(l => l.ReturnedQuantity > 0))
            throw new BusinessRuleException("Items from this sale were returned; it can no longer be voided.");
        if (sale.Shift is { Status: ShiftStatus.Closed })
            throw new BusinessRuleException("The shift for this sale is closed. Process a return instead.");

        foreach (var line in sale.Lines)
            StockLedger.Apply(db, line.ProductVariant!, line.Quantity, StockMovementType.Void, userId, sale.ReceiptNumber, "Sale voided");

        if (sale.Customer is { } customer)
        {
            customer.StoreCredit += sale.Payments.Where(p => p.Method == PaymentMethod.StoreCredit).Sum(p => p.Amount);
            customer.LoyaltyPoints += sale.LoyaltyPointsRedeemed;
            customer.LoyaltyPoints = Math.Max(0, customer.LoyaltyPoints - sale.LoyaltyPointsEarned);
        }

        sale.Status = SaleStatus.Voided;
        sale.VoidedAt = DateTime.Now;
        sale.VoidedByUserId = userId;
        sale.VoidReason = reason.Trim();
        await SaveAsync(db, ct);
    }

    // ---- Held (parked) carts -------------------------------------------------------------

    public async Task<HeldSale> HoldAsync(string label, int userId, HeldCart cart, CancellationToken ct = default)
    {
        if (cart.Lines.Count == 0) throw new BusinessRuleException("Nothing to hold — the cart is empty.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var held = new HeldSale
        {
            Label = string.IsNullOrWhiteSpace(label) ? $"Held {DateTime.Now:HH:mm}" : label.Trim(),
            UserId = userId,
            CustomerId = cart.CustomerId,
            CreatedAt = DateTime.Now,
            Payload = JsonSerializer.Serialize(cart),
        };
        db.HeldSales.Add(held);
        await db.SaveChangesAsync(ct);
        return held;
    }

    public async Task<List<HeldSale>> GetHeldAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.HeldSales.AsNoTracking().OrderBy(h => h.CreatedAt).ToListAsync(ct);
    }

    /// <summary>Returns the cart and deletes the held record.</summary>
    public async Task<HeldCart> ResumeAsync(int heldSaleId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var held = await db.HeldSales.FindAsync([heldSaleId], ct) ?? throw new BusinessRuleException("Held sale not found.");
        var cart = JsonSerializer.Deserialize<HeldCart>(held.Payload) ?? throw new BusinessRuleException("Held sale is corrupt.");
        db.HeldSales.Remove(held);
        await db.SaveChangesAsync(ct);
        return cart;
    }

    public async Task DeleteHeldAsync(int heldSaleId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.HeldSales.Where(h => h.Id == heldSaleId).ExecuteDeleteAsync(ct);
    }

    internal static IQueryable<Sale> SaleQuery(PosDbContext db) =>
        db.Sales.AsNoTracking()
            .Include(s => s.Lines)
            .Include(s => s.Payments)
            .Include(s => s.Customer)
            .Include(s => s.User)
            .AsSplitQuery();

    private static async Task SaveAsync(PosDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("Stock or customer balance changed at another till. Please try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new BusinessRuleException("Another till just used the same receipt number. Please try again.");
        }
    }
}
