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
    ManageUsers,
    ManageSettings,
    ViewAllShifts,
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
        Permission.ViewAllShifts,
    ];

    public static bool Has(UserRole role, Permission permission) => role switch
    {
        UserRole.Admin => true,
        UserRole.Manager => ManagerPermissions.Contains(permission),
        UserRole.Cashier => CashierPermissions.Contains(permission),
        _ => false,
    };
}
