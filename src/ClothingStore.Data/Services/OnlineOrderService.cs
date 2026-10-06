using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Localization;
using ClothingStore.Core.Pricing;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Data.Services;

public sealed record OnlineOrderLineInput(int VariantId, int Quantity);

/// <summary>What staff type in from the chat.</summary>
public sealed record OnlineOrderInput
{
    public SalesChannel Channel { get; init; } = SalesChannel.WhatsApp;
    public int? CustomerId { get; init; }
    public required string CustomerName { get; init; }
    public string? Phone { get; init; }
    public string? Handle { get; init; }
    public string? Address { get; init; }
    public string? Notes { get; init; }
    public decimal DeliveryFee { get; init; }
    public DiscountType DiscountType { get; init; }
    public decimal DiscountValue { get; init; }
    public required IReadOnlyList<OnlineOrderLineInput> Lines { get; init; }

    /// <summary>Add the person as a customer when nobody with that phone number is on file.</summary>
    public bool SaveCustomer { get; init; }
}

/// <summary>Payment collected when the order is delivered (same rules as the register).</summary>
public sealed record CompleteOnlineOrderRequest
{
    public required int UserId { get; init; }
    public int? ShiftId { get; init; }
    public required IReadOnlyList<PaymentInput> Payments { get; init; }
    public ChangeCurrency ChangeIn { get; init; }
    public decimal ExchangeRate { get; init; }
}

public sealed record OnlineOrderCounts(int New, int Confirmed, int OutForDelivery)
{
    public int Open => New + Confirmed + OutForDelivery;
}

/// <summary>
/// Orders taken by message or phone. Confirming holds the stock, completing records the sale (with the delivery fee)
/// without touching stock again, and cancelling puts held stock back.
/// </summary>
public class OnlineOrderService(IDbContextFactory<PosDbContext> factory)
{
    private const string Prefix = "OL";

    /// <summary>Raised after any order is created or changes status, so badges and lists can refresh.</summary>
    public event EventHandler? Changed;

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public async Task<OnlineOrder> CreateAsync(OnlineOrderInput input, int userId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var order = new OnlineOrder { CreatedAt = DateTime.Now, CreatedByUserId = userId, Status = OnlineOrderStatus.New };
        await FillAsync(db, order, input, ct);
        order.OrderNumber = await NextNumberAsync(db, order.CreatedAt, ct);
        db.OnlineOrders.Add(order);
        await SaveAsync(db, ct);
        OnChanged();
        return (await GetAsync(order.Id, ct))!;
    }

    /// <summary>Edits an order that hasn't been confirmed yet (confirmed orders hold stock; cancel and re-take them instead).</summary>
    public async Task<OnlineOrder> UpdateAsync(int orderId, OnlineOrderInput input, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var order = await db.OnlineOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new BusinessRuleException(Loc.T("Err.OrderNotFound"));
        if (order.Status != OnlineOrderStatus.New) throw new BusinessRuleException(Loc.T("Err.OrderNotEditable"));

        db.OnlineOrderLines.RemoveRange(order.Lines);
        order.Lines.Clear();
        await FillAsync(db, order, input, ct);
        await SaveAsync(db, ct);
        OnChanged();
        return (await GetAsync(order.Id, ct))!;
    }

    /// <summary>The customer confirmed: take the items out of stock so they can't be sold to someone else.</summary>
    public async Task ConfirmAsync(int orderId, int userId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var order = await LoadForChangeAsync(db, orderId, ct);
        if (order.Status != OnlineOrderStatus.New) throw new BusinessRuleException(Loc.T("Err.OrderWrongStatus"));

        await HoldStockAsync(db, order, userId, ct);
        order.Status = OnlineOrderStatus.Confirmed;
        order.ConfirmedAt = DateTime.Now;
        await SaveAsync(db, ct);
        OnChanged();
    }

    public async Task MarkOutForDeliveryAsync(int orderId, string? courier, int userId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var order = await LoadForChangeAsync(db, orderId, ct);
        if (order.Status is not (OnlineOrderStatus.New or OnlineOrderStatus.Confirmed))
            throw new BusinessRuleException(Loc.T("Err.OrderWrongStatus"));

        if (!order.StockHeld) await HoldStockAsync(db, order, userId, ct);
        order.ConfirmedAt ??= DateTime.Now;
        order.Status = OnlineOrderStatus.OutForDelivery;
        order.ShippedAt = DateTime.Now;
        order.Courier = QueryHelpers.Clean(courier);
        await SaveAsync(db, ct);
        OnChanged();
    }

    /// <summary>The money is in: records the sale (items + delivery fee) and closes the order.</summary>
    public async Task<Sale> CompleteAsync(int orderId, CompleteOnlineOrderRequest request, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var settings = await db.Settings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct) ?? new StoreSettings();
        var order = await LoadForChangeAsync(db, orderId, ct);
        if (!order.IsOpen) throw new BusinessRuleException(Loc.T("Err.OrderWrongStatus"));

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive, ct)
                   ?? throw new BusinessRuleException(Loc.T("Err.CashierInactive"));

        Shift? shift = null;
        if (request.ShiftId is { } shiftId)
        {
            shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId, ct);
            if (shift is not { Status: ShiftStatus.Open }) throw new BusinessRuleException(Loc.T("Err.ShiftClosedSell"));
        }
        if (shift is null && request.Payments.Any(p => SalesService.IsCash(p.Method) && p.Amount > 0))
            throw new BusinessRuleException(Loc.T("Err.OrderCashNeedsShift"));

        Customer? customer = order.CustomerId is { } cid ? await db.Customers.FirstOrDefaultAsync(c => c.Id == cid, ct) : null;

        if (!order.StockHeld) await HoldStockAsync(db, order, user.Id, ct);

        var now = DateTime.Now;
        var sale = new Sale
        {
            CreatedAt = now,
            UserId = user.Id,
            CustomerId = customer?.Id,
            ShiftId = shift?.Id,
            Subtotal = order.Subtotal,
            DiscountTotal = order.DiscountTotal,
            TaxTotal = order.TaxTotal,
            Total = order.Total,
            DeliveryFee = order.DeliveryFee,
            CartDiscountType = order.DiscountType,
            CartDiscountValue = order.DiscountValue,
            Channel = order.Channel,
            Notes = Loc.T("Orders.SaleNote", order.OrderNumber),
            Status = SaleStatus.Completed,
        };
        SalesService.ApplyPayments(sale, request.Payments, customer, settings, request.ChangeIn, request.ExchangeRate);
        sale.ReceiptNumber = await SalesService.NextReceiptNumberAsync(db, settings.ReceiptPrefix, now, ct);

        // Stock already left with the order, so the sale lines don't move it again.
        foreach (var l in order.Lines)
        {
            sale.Lines.Add(new SaleLine
            {
                ProductVariantId = l.ProductVariantId,
                ProductName = l.ProductName,
                VariantDescription = l.VariantDescription,
                Sku = l.Sku,
                CategoryName = l.CategoryName,
                UnitPrice = l.UnitPrice,
                UnitCost = l.UnitCost,
                Quantity = l.Quantity,
                DiscountAmount = l.DiscountAmount,
                TaxAmount = l.TaxAmount,
                LineTotal = l.LineTotal,
            });
        }

        db.Sales.Add(sale);
        order.Sale = sale;
        order.Status = OnlineOrderStatus.Completed;
        order.CompletedAt = now;
        order.StockHeld = false;
        await SalesService.SaveAsync(db, ct);
        OnChanged();
        return (await new SalesService(factory).GetAsync(sale.Id, ct))!;
    }

    public async Task CancelAsync(int orderId, string reason, int userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleException(Loc.T("Err.EnterReason"));
        await using var db = await factory.CreateDbContextAsync(ct);
        var order = await LoadForChangeAsync(db, orderId, ct);
        if (!order.IsOpen) throw new BusinessRuleException(Loc.T("Err.OrderWrongStatus"));

        if (order.StockHeld)
        {
            foreach (var line in order.Lines)
                StockLedger.Apply(db, line.ProductVariant!, line.Quantity, StockMovementType.OnlineOrderCancelled, userId, order.OrderNumber, reason.Trim());
            order.StockHeld = false;
        }
        order.Status = OnlineOrderStatus.Cancelled;
        order.CancelledAt = DateTime.Now;
        order.CancelReason = reason.Trim();
        await SaveAsync(db, ct);
        OnChanged();
    }

    public async Task<OnlineOrder?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.OnlineOrders.AsNoTracking()
            .Include(o => o.Lines)
            .Include(o => o.Customer)
            .Include(o => o.CreatedBy)
            .Include(o => o.Sale)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    /// <summary>
    /// Orders by status (null = all, or every open one with <paramref name="openOnly"/>), newest first;
    /// text matches number, name, phone, handle or address.
    /// </summary>
    public async Task<List<OnlineOrder>> SearchAsync(
        OnlineOrderStatus? status, string? text = null, bool openOnly = false, int take = 500, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.OnlineOrders.AsNoTracking().Include(o => o.Lines).AsSplitQuery().AsQueryable();
        if (status is { } s) query = query.Where(o => o.Status == s);
        else if (openOnly)
            query = query.Where(o => o.Status == OnlineOrderStatus.New || o.Status == OnlineOrderStatus.Confirmed || o.Status == OnlineOrderStatus.OutForDelivery);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var pattern = QueryHelpers.LikePattern(text);
            query = query.Where(o =>
                EF.Functions.Like(o.OrderNumber, pattern, "\\") ||
                EF.Functions.Like(o.CustomerName, pattern, "\\") ||
                EF.Functions.Like(o.Phone!, pattern, "\\") ||
                EF.Functions.Like(o.Handle!, pattern, "\\") ||
                EF.Functions.Like(o.Address!, pattern, "\\"));
        }
        return await query.OrderByDescending(o => o.CreatedAt).Take(take).ToListAsync(ct);
    }

    public async Task<OnlineOrderCounts> GetCountsAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var counts = await db.OnlineOrders.AsNoTracking()
            .Where(o => o.Status == OnlineOrderStatus.New || o.Status == OnlineOrderStatus.Confirmed || o.Status == OnlineOrderStatus.OutForDelivery)
            .GroupBy(o => o.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        int Count(OnlineOrderStatus s) => counts.FirstOrDefault(c => c.Key == s)?.Count ?? 0;
        return new OnlineOrderCounts(Count(OnlineOrderStatus.New), Count(OnlineOrderStatus.Confirmed), Count(OnlineOrderStatus.OutForDelivery));
    }

    // ---- Internals --------------------------------------------------------------------------

    private static async Task FillAsync(PosDbContext db, OnlineOrder order, OnlineOrderInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.CustomerName)) throw new BusinessRuleException(Loc.T("Err.OrderNeedsName"));
        var lines = input.Lines.Where(l => l.Quantity > 0).ToList();
        if (lines.Count == 0) throw new BusinessRuleException(Loc.T("Err.CartEmpty"));
        if (input.DeliveryFee < 0) throw new BusinessRuleException(Loc.T("Err.AmountZero"));
        if (input.Channel == SalesChannel.InStore) throw new BusinessRuleException(Loc.T("Err.OrderChannel"));

        var settings = await db.Settings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct) ?? new StoreSettings();
        var ids = lines.Select(l => l.VariantId).Distinct().ToList();
        var variants = await db.ProductVariants.AsNoTracking()
            .Include(v => v.Product).ThenInclude(p => p!.Category)
            .Where(v => ids.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);
        if (lines.Any(l => !variants.TryGetValue(l.VariantId, out var v) || !v.IsActive || !v.Product!.IsActive))
            throw new BusinessRuleException(Loc.T("Err.ItemUnavailable"));

        var totals = CartCalculator.Calculate(
            lines.Select(l => new CartLineInput(variants[l.VariantId].EffectivePrice, l.Quantity)).ToList(),
            input.DiscountType, input.DiscountValue, settings.TaxRate, settings.PricesIncludeTax);

        var phone = QueryHelpers.Clean(input.Phone);
        var customerId = input.CustomerId;
        if (customerId is null && phone is not null)
        {
            customerId = await db.Customers.Where(c => c.Phone == phone).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
            if (customerId is null && input.SaveCustomer)
            {
                var name = input.CustomerName.Trim();
                var space = name.IndexOf(' ');
                var customer = new Customer
                {
                    FirstName = space > 0 ? name[..space] : name,
                    LastName = space > 0 ? name[(space + 1)..].Trim() : "",
                    Phone = phone,
                    Notes = QueryHelpers.Clean(input.Address),
                };
                db.Customers.Add(customer);
                order.Customer = customer;
            }
        }

        order.Channel = input.Channel;
        if (order.Customer is null) order.CustomerId = customerId; // a new customer gets its id on save
        order.CustomerName = input.CustomerName.Trim();
        order.Phone = phone;
        order.Handle = QueryHelpers.Clean(input.Handle);
        order.Address = QueryHelpers.Clean(input.Address);
        order.Notes = QueryHelpers.Clean(input.Notes);
        order.DiscountType = input.DiscountType;
        order.DiscountValue = input.DiscountType == DiscountType.None ? 0 : input.DiscountValue;
        order.Subtotal = totals.Subtotal;
        order.DiscountTotal = totals.DiscountTotal;
        order.TaxTotal = totals.TaxTotal;
        order.ItemsTotal = totals.Total;
        order.DeliveryFee = Money.Round(input.DeliveryFee);
        order.Total = totals.Total + order.DeliveryFee;

        for (var i = 0; i < lines.Count; i++)
        {
            var v = variants[lines[i].VariantId];
            var r = totals.Lines[i];
            order.Lines.Add(new OnlineOrderLine
            {
                ProductVariantId = v.Id,
                ProductName = v.Product!.Name,
                VariantDescription = v.Description,
                Sku = v.Sku,
                CategoryName = v.Product.Category?.Name,
                UnitPrice = v.EffectivePrice,
                UnitCost = v.EffectiveCost,
                Quantity = lines[i].Quantity,
                DiscountAmount = r.Discount,
                TaxAmount = r.Tax,
                LineTotal = r.Total,
            });
        }
    }

    private static async Task HoldStockAsync(PosDbContext db, OnlineOrder order, int userId, CancellationToken ct)
    {
        var allowNegative = await db.Settings.AsNoTracking().Select(s => s.AllowNegativeStock).FirstOrDefaultAsync(ct);
        foreach (var line in order.Lines)
            StockLedger.Apply(db, line.ProductVariant!, -line.Quantity, StockMovementType.OnlineOrder, userId, order.OrderNumber,
                allowNegative: allowNegative);
        order.StockHeld = true;
    }

    private static async Task<OnlineOrder> LoadForChangeAsync(PosDbContext db, int orderId, CancellationToken ct) =>
        await db.OnlineOrders
            .Include(o => o.Lines).ThenInclude(l => l.ProductVariant)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
        ?? throw new BusinessRuleException(Loc.T("Err.OrderNotFound"));

    private static async Task<string> NextNumberAsync(PosDbContext db, DateTime date, CancellationToken ct)
    {
        var stem = QueryHelpers.DayStem(Prefix, date);
        var last = await db.OnlineOrders
            .Where(o => o.OrderNumber.StartsWith(stem))
            .OrderByDescending(o => o.OrderNumber)
            .Select(o => o.OrderNumber)
            .FirstOrDefaultAsync(ct);
        return QueryHelpers.NextNumber(Prefix, date, last);
    }

    private static async Task SaveAsync(PosDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException(Loc.T("Err.ConcurrentSale"));
        }
        catch (DbUpdateException ex) when (QueryHelpers.IsUniqueViolation(ex))
        {
            throw new BusinessRuleException(Loc.T("Err.ReceiptNumberTaken"));
        }
    }
}
