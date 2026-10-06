namespace ClothingStore.Core.Entities;

public class Sale : Entity
{
    public string ReceiptNumber { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public SaleStatus Status { get; set; } = SaleStatus.Completed;

    public int UserId { get; set; }
    public User? User { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? ShiftId { get; set; }
    public Shift? Shift { get; set; }

    /// <summary>Sum of unit price x quantity before any discount.</summary>
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal Total { get; set; }

    public DiscountType CartDiscountType { get; set; }
    public decimal CartDiscountValue { get; set; }

    /// <summary>Dollars handed over by the customer (may exceed the cash applied).</summary>
    public decimal CashTendered { get; set; }

    /// <summary>Dollars handed back as change.</summary>
    public decimal ChangeGiven { get; set; }

    /// <summary>Lebanese pounds handed over by the customer.</summary>
    public decimal CashTenderedLbp { get; set; }

    /// <summary>Lebanese pounds handed back as change.</summary>
    public decimal ChangeGivenLbp { get; set; }

    /// <summary>LBP per dollar when the sale was made; 0 when LBP was not in use.</summary>
    public decimal ExchangeRate { get; set; }

    public int LoyaltyPointsEarned { get; set; }
    public int LoyaltyPointsRedeemed { get; set; }

    public string? Notes { get; set; }

    /// <summary>In store, or the channel of the online order it completed.</summary>
    public SalesChannel Channel { get; set; }

    /// <summary>Delivery charge included in <see cref="Total"/> (not in the lines, not taxed).</summary>
    public decimal DeliveryFee { get; set; }

    /// <summary>Delivery company or driver of an online order.</summary>
    public string? Courier { get; set; }

    /// <summary>
    /// When part of the sale was paid with <see cref="PaymentMethod.Delivery"/>: the payment from the delivery company
    /// that covered it. Null while the money is still owed.
    /// </summary>
    public int? DeliverySettlementId { get; set; }
    public DeliverySettlement? DeliverySettlement { get; set; }

    public DateTime? VoidedAt { get; set; }
    public int? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }

    public List<SaleLine> Lines { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];

    public int ItemCount => Lines.Sum(l => l.Quantity);
}

public class SaleLine : Entity
{
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }

    public int ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    // Snapshots so receipts/reports stay correct even if the product is later edited.
    public string ProductName { get; set; } = "";
    public string VariantDescription { get; set; } = "";
    public string Sku { get; set; } = "";
    public string? CategoryName { get; set; }

    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public int Quantity { get; set; }

    /// <summary>Line discount + share of the cart discount.</summary>
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }

    /// <summary>Amount the customer paid for this line (tax included).</summary>
    public decimal LineTotal { get; set; }

    public int ReturnedQuantity { get; set; }

    public int ReturnableQuantity => Quantity - ReturnedQuantity;
}

public class Payment : Entity
{
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }

    public PaymentMethod Method { get; set; }

    /// <summary>Amount applied to the sale (for cash this excludes change).</summary>
    public decimal Amount { get; set; }

    public string? Reference { get; set; }
}
