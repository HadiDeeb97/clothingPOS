using ClothingStore.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Data;

public class PosDbContext(DbContextOptions<PosDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<SaleReturn> Returns => Set<SaleReturn>();
    public DbSet<SaleReturnLine> ReturnLines => Set<SaleReturnLine>();
    public DbSet<SaleReturnRefund> ReturnRefunds => Set<SaleReturnRefund>();
    public DbSet<HeldSale> HeldSales => Set<HeldSale>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<StoreSettings> Settings => Set<StoreSettings>();
    public DbSet<BackupRecord> BackupRecords => Set<BackupRecord>();
    public DbSet<ExchangeRateChange> ExchangeRateChanges => Set<ExchangeRateChange>();

    /// <summary>
    /// Usernames, SKUs and category/supplier names are unique regardless of case, even if the
    /// database was created with a case-sensitive default collation.
    /// </summary>
    private const string CaseInsensitive = "Latin1_General_100_CI_AS";

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Money (prices, costs, totals, balances) is stored to the cent; rates override this below.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.Property(x => x.Username).HasMaxLength(50).UseCollation(CaseInsensitive).IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            e.Property(x => x.PreferredLanguage).HasMaxLength(10);
        });

        b.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).UseCollation(CaseInsensitive).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Supplier>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).UseCollation(CaseInsensitive).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Product>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Name);
            e.HasOne(x => x.Category).WithMany(c => c.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Variants).WithOne(v => v.Product).HasForeignKey(v => v.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ProductVariant>(e =>
        {
            e.Property(x => x.Sku).HasMaxLength(64).UseCollation(CaseInsensitive).IsRequired();
            e.HasIndex(x => x.Sku).IsUnique();
            e.Property(x => x.Barcode).HasMaxLength(64);
            e.HasIndex(x => x.Barcode).IsUnique().HasFilter("[Barcode] IS NOT NULL");
            e.Property(x => x.Size).HasMaxLength(20);
            e.Property(x => x.Color).HasMaxLength(40);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.Ignore(x => x.EffectivePrice);
            e.Ignore(x => x.EffectiveCost);
            e.Ignore(x => x.IsLowStock);
            e.Ignore(x => x.Description);
            e.Ignore(x => x.DisplayName);
        });

        b.Entity<Customer>(e =>
        {
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.HasIndex(x => x.Phone).IsUnique().HasFilter("[Phone] IS NOT NULL");
            e.Property(x => x.Email).HasMaxLength(150);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.Ignore(x => x.FullName);
        });

        b.Entity<Sale>(e =>
        {
            e.Property(x => x.ReceiptNumber).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.ReceiptNumber).IsUnique();
            e.HasIndex(x => x.CreatedAt);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Lines).WithOne(l => l.Sale).HasForeignKey(l => l.SaleId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Payments).WithOne(p => p.Sale).HasForeignKey(p => p.SaleId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(x => x.ItemCount);
        });

        b.Entity<SaleLine>(e =>
        {
            e.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
            // Two tills returning the same item at once: the second save fails instead of refunding it twice.
            e.Property(x => x.ReturnedQuantity).IsConcurrencyToken();
            e.Ignore(x => x.ReturnableQuantity);
        });

        b.Entity<SaleReturn>(e =>
        {
            e.Property(x => x.ReturnNumber).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.ReturnNumber).IsUnique();
            e.HasIndex(x => x.CreatedAt);
            e.HasOne(x => x.Sale).WithMany().HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Lines).WithOne(l => l.SaleReturn).HasForeignKey(l => l.SaleReturnId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Refunds).WithOne(r => r.SaleReturn).HasForeignKey(r => r.SaleReturnId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SaleReturnLine>(e =>
            e.HasOne(x => x.SaleLine).WithMany().HasForeignKey(x => x.SaleLineId).OnDelete(DeleteBehavior.Restrict));

        b.Entity<StockMovement>(e =>
        {
            e.HasIndex(x => new { x.ProductVariantId, x.CreatedAt });
            e.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<PurchaseOrder>(e =>
        {
            e.Property(x => x.OrderNumber).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Lines).WithOne(l => l.PurchaseOrder).HasForeignKey(l => l.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(x => x.Total);
            e.Ignore(x => x.TotalUnits);
        });

        b.Entity<PurchaseOrderLine>(e =>
        {
            e.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.LineTotal);
            e.Ignore(x => x.QuantityOutstanding);
        });

        b.Entity<Shift>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Status });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.CashMovements).WithOne(m => m.Shift).HasForeignKey(m => m.ShiftId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(x => x.Variance);
            e.Ignore(x => x.VarianceLbp);
        });

        b.Entity<HeldSale>(e => e.Property(x => x.Label).HasMaxLength(100));

        b.Entity<BackupRecord>(e =>
        {
            e.HasIndex(x => x.StartedAt);
            e.Property(x => x.FilePath).HasMaxLength(500);
            e.Property(x => x.Error).HasMaxLength(2000);
        });

        b.Entity<StoreSettings>(e =>
        {
            e.Property(x => x.TaxRate).HasPrecision(9, 4);
            e.Property(x => x.MaxCashierDiscountPercent).HasPrecision(9, 4);
            e.Property(x => x.LoyaltyPointsPerUnit).HasPrecision(18, 4);
            e.Property(x => x.LoyaltyPointValue).HasPrecision(18, 4);
            e.Property(x => x.ReceiptLanguage).HasMaxLength(10);
            e.Ignore(x => x.ActiveLbpRate);
        });

        b.Entity<ExchangeRateChange>(e =>
        {
            e.HasIndex(x => x.ChangedAt);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        BumpVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        BumpVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void BumpVersions()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Modified))
        {
            switch (entry.Entity)
            {
                case ProductVariant v: v.Version++; break;
                case Customer c: c.Version++; break;
            }
        }
    }
}
