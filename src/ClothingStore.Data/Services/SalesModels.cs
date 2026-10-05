using ClothingStore.Core;
using ClothingStore.Core.Pricing;

namespace ClothingStore.Data.Services;

public sealed record CheckoutLine(int VariantId, int Quantity, DiscountType DiscountType = DiscountType.None, decimal DiscountValue = 0m);

public sealed record PaymentInput(PaymentMethod Method, decimal Amount, string? Reference = null);

public sealed record CheckoutRequest
{
    public required int UserId { get; init; }
    public int? ShiftId { get; init; }
    public int? CustomerId { get; init; }
    public required IReadOnlyList<CheckoutLine> Lines { get; init; }
    public DiscountType CartDiscountType { get; init; }
    public decimal CartDiscountValue { get; init; }

    /// <summary>
    /// Tenders. For cash, Amount is what the customer handed over; change is worked out here.
    /// For every other method Amount is the amount charged.
    /// </summary>
    public required IReadOnlyList<PaymentInput> Payments { get; init; }

    /// <summary>Manager who approved a discount above the cashier limit (if any).</summary>
    public int? ApprovedByUserId { get; init; }

    public string? Notes { get; init; }
}

public sealed record HeldCartLine(int VariantId, int Quantity, DiscountType DiscountType, decimal DiscountValue);

public sealed record HeldCart(
    IReadOnlyList<HeldCartLine> Lines,
    DiscountType CartDiscountType,
    decimal CartDiscountValue,
    int? CustomerId);

public sealed record ReturnLineRequest(int SaleLineId, int Quantity, bool Restock = true);

public sealed record ReturnRequest
{
    public required int SaleId { get; init; }
    public required int UserId { get; init; }
    public int? ShiftId { get; init; }
    public RefundDestination RefundTo { get; init; } = RefundDestination.OriginalPayment;
    public required IReadOnlyList<ReturnLineRequest> Lines { get; init; }
    public string? Reason { get; init; }

    /// <summary>Manager who approved a return outside the return window or a cash refund of a card payment (if any).</summary>
    public int? ApprovedByUserId { get; init; }
}

/// <summary>What a return would refund and how, before it is processed.</summary>
public sealed record RefundPlan(decimal Total, IReadOnlyList<RefundShare> Shares)
{
    public decimal CashOut => Shares.Where(s => s.Method == RefundMethod.Cash).Sum(s => s.Amount);
    public bool NeedsCashOverride => Shares.Any(s => s.IsCashOverride);
    public bool AddsStoreCredit => Shares.Any(s => s.Method == RefundMethod.StoreCredit);
}
