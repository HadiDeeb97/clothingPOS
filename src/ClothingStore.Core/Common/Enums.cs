namespace ClothingStore.Core;

public enum UserRole
{
    Cashier = 0,
    Manager = 1,
    Admin = 2,
}

public enum Gender
{
    Unisex = 0,
    Men = 1,
    Women = 2,
    Kids = 3,
}

public enum SaleStatus
{
    Completed = 0,
    Voided = 1,
}

public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    MobileWallet = 2,
    StoreCredit = 3,
    LoyaltyPoints = 4,
}

public enum RefundMethod
{
    Cash = 0,
    Card = 1,
    StoreCredit = 2,
}

public enum DiscountType
{
    None = 0,
    Percent = 1,
    Amount = 2,
}

public enum StockMovementType
{
    InitialStock = 0,
    Sale = 1,
    Return = 2,
    PurchaseReceipt = 3,
    Adjustment = 4,
    Damaged = 5,
    Void = 6,
    StockCount = 7,
}

public enum PurchaseOrderStatus
{
    Draft = 0,
    Ordered = 1,
    PartiallyReceived = 2,
    Received = 3,
    Cancelled = 4,
}

public enum ShiftStatus
{
    Open = 0,
    Closed = 1,
}

public enum CashMovementType
{
    PayIn = 0,
    PayOut = 1,
}
