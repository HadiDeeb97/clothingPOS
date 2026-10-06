namespace ClothingStore.Core.Entities;

/// <summary>
/// An order taken over WhatsApp, Instagram, Facebook or the phone. It is confirmed (stock held), sent out for
/// delivery, and completed when the money comes back, which records a normal <see cref="Sale"/>.
/// </summary>
public class OnlineOrder : Entity
{
    public string OrderNumber { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    public SalesChannel Channel { get; set; } = SalesChannel.WhatsApp;
    public OnlineOrderStatus Status { get; set; } = OnlineOrderStatus.New;

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    // Delivery details as given in the chat (the customer may not be on file).
    public string CustomerName { get; set; } = "";
    public string? Phone { get; set; }

    /// <summary>Instagram / Facebook username, if the order came that way.</summary>
    public string? Handle { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }

    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }

    /// <summary>Items before discounts.</summary>
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }

    /// <summary>What the items cost after discounts (tax included).</summary>
    public decimal ItemsTotal { get; set; }
    public decimal DeliveryFee { get; set; }

    /// <summary>To collect: items + delivery.</summary>
    public decimal Total { get; set; }

    /// <summary>True while items are taken out of stock for this order.</summary>
    public bool StockHeld { get; set; }

    public string? Courier { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    /// <summary>The sale recorded when the order was completed.</summary>
    public int? SaleId { get; set; }
    public Sale? Sale { get; set; }

    public List<OnlineOrderLine> Lines { get; set; } = [];

    public int ItemCount => Lines.Sum(l => l.Quantity);
    public bool IsOpen => Status is OnlineOrderStatus.New or OnlineOrderStatus.Confirmed or OnlineOrderStatus.OutForDelivery;
}

public class OnlineOrderLine : Entity
{
    public int OnlineOrderId { get; set; }
    public OnlineOrder? OnlineOrder { get; set; }

    public int ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    // Snapshots, like sale lines.
    public string ProductName { get; set; } = "";
    public string VariantDescription { get; set; } = "";
    public string Sku { get; set; } = "";
    public string? CategoryName { get; set; }

    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public int Quantity { get; set; }

    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
}
