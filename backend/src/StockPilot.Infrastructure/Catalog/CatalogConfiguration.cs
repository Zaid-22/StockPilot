using Microsoft.EntityFrameworkCore;
using StockPilot.Domain.Catalog;

namespace StockPilot.Infrastructure.Catalog;

public static class CatalogConfiguration
{
    public static void ConfigureCatalog(this ModelBuilder model)
    {
        ConfigureRecord<Category>(model, "Categories");
        ConfigureRecord<Supplier>(model, "Suppliers");
        ConfigureRecord<Customer>(model, "Customers");
        model.Entity<Category>().HasIndex(x => x.NormalizedName).IsUnique();
        ConfigureContact<Supplier>(model);
        ConfigureContact<Customer>(model);
        var product = model.Entity<Product>();
        product.ToTable("Products", table =>
        {
            table.HasCheckConstraint("CK_Products_SellingPrice", "[SellingPrice] >= 0");
            table.HasCheckConstraint("CK_Products_ReorderLevel", "[ReorderLevel] >= 0");
        });
        product.HasKey(x => x.Id);
        product.Property(x => x.Sku).HasMaxLength(64);
        product.Property(x => x.NormalizedSku).HasMaxLength(64);
        product.HasIndex(x => x.NormalizedSku).IsUnique();
        product.Property(x => x.Name).HasMaxLength(160);
        product.Property(x => x.Description).HasMaxLength(2000);
        product.Property(x => x.SellingPrice).HasPrecision(18, 3);
        product.Property(x => x.Version).IsConcurrencyToken();
        product.HasIndex(x => new { x.IsActive, x.Name, x.Id });
        product.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        var warehouse = model.Entity<Warehouse>();
        warehouse.ToTable("Warehouses");
        warehouse.HasKey(x => x.Id);
        warehouse.Property(x => x.Name).HasMaxLength(160);
        warehouse.HasData(new Warehouse { Id = Warehouse.MainId, Name = "Main warehouse" });
        var balance = model.Entity<InventoryBalance>();
        balance.ToTable("InventoryBalances", table => table.HasCheckConstraint("CK_Balances_Nonnegative", "[QuantityOnHand] >= 0"));
        balance.HasKey(x => new { x.ProductId, x.WarehouseId });
        balance.HasOne(x => x.Product).WithMany(x => x.Balances).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        balance.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureRecord<T>(ModelBuilder model, string table) where T : class, INamedRecord
    {
        var entity = model.Entity<T>();
        entity.ToTable(table);
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Name).HasMaxLength(160);
        entity.Property(x => x.NormalizedName).HasMaxLength(160);
        entity.Property(x => x.Version).IsConcurrencyToken();
        entity.HasIndex(x => new { x.IsActive, x.Name, x.Id });
    }
    private static void ConfigureContact<T>(ModelBuilder model) where T : class, IContactRecord
    {
        model.Entity<T>().Property(x => x.Email).HasMaxLength(256);
        model.Entity<T>().Property(x => x.Phone).HasMaxLength(40);
        model.Entity<T>().Property(x => x.Address).HasMaxLength(500);
    }
}
