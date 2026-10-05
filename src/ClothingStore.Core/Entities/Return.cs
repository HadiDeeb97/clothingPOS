namespace ClothingStore.Core.Entities;

public class SaleReturn : Entity
{
    public string ReturnNumber { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public int SaleId { get; set; }
    public Sale? Sale { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? ShiftId { get; set; }

    public RefundMethod RefundMethod { get; set; }
    public decimal TotalRefund { get; set; }
    public decimal TaxRefund { get; set; }
    public string? Reason { get; set; }

    public List<SaleReturnLine> Lines { get; set; } = [];
}

public class SaleReturnLine : Entity
{
    public int SaleReturnId { get; set; }
    public SaleReturn? SaleReturn { get; set; }

    public int SaleLineId { get; set; }
    public SaleLine? SaleLine { get; set; }

    public int Quantity { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public bool Restocked { get; set; }
}
