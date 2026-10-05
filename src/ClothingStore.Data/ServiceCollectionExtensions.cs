using ClothingStore.Data.Seeding;
using ClothingStore.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClothingStore.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPosData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<PosDbContext>(o => o.UseSqlServer(connectionString));

        // Services are stateless (each call opens its own short-lived DbContext) so singletons are safe.
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<UserService>();
        services.AddSingleton<CategoryService>();
        services.AddSingleton<SupplierService>();
        services.AddSingleton<ProductService>();
        services.AddSingleton<InventoryService>();
        services.AddSingleton<CustomerService>();
        services.AddSingleton<SalesService>();
        services.AddSingleton<ReturnService>();
        services.AddSingleton<ShiftService>();
        services.AddSingleton<PurchaseOrderService>();
        services.AddSingleton<ReportService>();
        services.AddSingleton<BackupService>();
        return services;
    }
}
