namespace ClothingStore.Core.Security;

public enum Permission
{
    Sell,
    ProcessReturns,
    ManageCustomers,
    ViewProducts,
    ManageProducts,
    ManageInventory,
    ManagePurchasing,
    ViewReports,
    VoidSales,
    AdjustStoreCredit,
    OverrideDiscountLimit,

    /// <summary>Refund card or wallet payments in cash.</summary>
    OverrideRefundMethod,
    ManageUsers,
    ManageSettings,
    ViewAllShifts,

    /// <summary>Change the LBP exchange rate.</summary>
    ChangeExchangeRate,
}

public static class Permissions
{
    private static readonly HashSet<Permission> CashierPermissions =
    [
        Permission.Sell,
        Permission.ProcessReturns,
        Permission.ManageCustomers,
        Permission.ViewProducts,
    ];

    private static readonly HashSet<Permission> ManagerPermissions =
    [
        .. CashierPermissions,
        Permission.ManageProducts,
        Permission.ManageInventory,
        Permission.ManagePurchasing,
        Permission.ViewReports,
        Permission.VoidSales,
        Permission.AdjustStoreCredit,
        Permission.OverrideDiscountLimit,
        Permission.OverrideRefundMethod,
        Permission.ViewAllShifts,
        Permission.ChangeExchangeRate,
    ];

    public static bool Has(UserRole role, Permission permission) => role switch
    {
        UserRole.Admin => true,
        UserRole.Manager => ManagerPermissions.Contains(permission),
        UserRole.Cashier => CashierPermissions.Contains(permission),
        _ => false,
    };
}
