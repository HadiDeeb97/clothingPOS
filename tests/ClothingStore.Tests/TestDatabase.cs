using ClothingStore.Core;
using ClothingStore.Core.Entities;
using ClothingStore.Core.Security;
using ClothingStore.Data;
using ClothingStore.Data.Seeding;
using ClothingStore.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Tests;

/// <summary>Fresh, migrated in-memory SQLite database per test with the real services wired up.</summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    public IDbContextFactory<PosDbContext> Factory { get; }

    public SettingsService Settings { get; }
    public UserService Users { get; }
    public CategoryService Categories { get; }
    public ProductService Products { get; }
    public InventoryService Inventory { get; }
    public CustomerService Customers { get; }
    public SalesService Sales { get; }
    public ReturnService Returns { get; }
    public ShiftService Shifts { get; }
    public PurchaseOrderService PurchaseOrders { get; }
    public SupplierService Suppliers { get; }
    public ReportService Reports { get; }

    public User Admin { get; private set; } = null!;
    public User Cashier { get; private set; } = null!;
    public User Manager { get; private set; } = null!;

    private TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<PosDbContext>().UseSqlite(_connection).Options;
        Factory = new ContextFactory(options);

        Settings = new SettingsService(Factory);
        Users = new UserService(Factory);
        Categories = new CategoryService(Factory);
        Products = new ProductService(Factory);
        Inventory = new InventoryService(Factory);
        Customers = new CustomerService(Factory);
        Sales = new SalesService(Factory);
        Returns = new ReturnService(Factory);
        Shifts = new ShiftService(Factory);
        PurchaseOrders = new PurchaseOrderService(Factory);
        Suppliers = new SupplierService(Factory);
        Reports = new ReportService(Factory);
    }

    public static async Task<TestDatabase> CreateAsync(Action<StoreSettings>? configure = null)
    {
        var db = new TestDatabase();
        await new DatabaseInitializer(db.Factory).InitializeAsync(seedDemoData: false);

        var settings = await db.Settings.GetAsync();
        settings.TaxRate = 10m;
        settings.PricesIncludeTax = false;
        settings.LoyaltyPointsPerUnit = 1m;
        settings.LoyaltyPointValue = 0.10m;
        settings.MaxCashierDiscountPercent = 10m;
        configure?.Invoke(settings);
        await db.Settings.SaveAsync(settings);

        db.Admin = (await db.Users.GetAllAsync()).Single();
        db.Cashier = await db.Users.SaveAsync(new User { Username = "cashier", FullName = "Casey Cashier", Role = UserRole.Cashier }, "secret1");
        db.Manager = await db.Users.SaveAsync(new User { Username = "manager", FullName = "Morgan Manager", Role = UserRole.Manager }, "secret2");
        return db;
    }

    /// <summary>Creates a T-shirt in S/M/L (Black) with the given stock per variant and price 20.00.</summary>
    public async Task<Product> CreateTeeAsync(int stockEach = 10, decimal price = 20m, decimal cost = 8m)
    {
        var category = await Categories.SaveAsync(new Category { Name = "Tees " + Guid.NewGuid().ToString("N")[..6] });
        var product = new Product
        {
            Name = "Basic Tee",
            CategoryId = category.Id,
            Price = price,
            Cost = cost,
            Variants =
            [
                new ProductVariant { Size = "S", Color = "Black", StockQuantity = stockEach },
                new ProductVariant { Size = "M", Color = "Black", StockQuantity = stockEach },
                new ProductVariant { Size = "L", Color = "Black", StockQuantity = stockEach },
            ],
        };
        return await Products.SaveAsync(product, Admin.Id);
    }

    public async Task<Shift> OpenShiftAsync(User? user = null, decimal openingFloat = 100m) =>
        await Shifts.OpenShiftAsync((user ?? Cashier).Id, openingFloat);

    public async Task<ProductVariant> GetVariantAsync(int id)
    {
        await using var ctx = await Factory.CreateDbContextAsync();
        return await ctx.ProductVariants.AsNoTracking().SingleAsync(v => v.Id == id);
    }

    public async Task<Customer> GetCustomerAsync(int id) => (await Customers.GetAsync(id))!;

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private sealed class ContextFactory(DbContextOptions<PosDbContext> options) : IDbContextFactory<PosDbContext>
    {
        public PosDbContext CreateDbContext() => new(options);
    }
}
