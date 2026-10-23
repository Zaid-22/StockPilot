using Microsoft.EntityFrameworkCore;
using StockPilot.Domain.Catalog;
using StockPilot.Domain.Inventory;
using StockPilot.Domain.Orders;

namespace StockPilot.Infrastructure.Orders;

public static class OrderConfiguration
{
    public static void ConfigureOrders(this ModelBuilder model)
    {
        var order = model.Entity<Order>();
        order.ToTable("Orders", t => t.HasCheckConstraint("CK_Orders_Party",
            "([Kind] = 'Purchase' AND [SupplierId] IS NOT NULL AND [CustomerId] IS NULL) OR ([Kind] = 'Sale' AND [CustomerId] IS NOT NULL AND [SupplierId] IS NULL)"));
        order.HasKey(x => x.Id);
        order.Property(x => x.Number).HasMaxLength(40);
        order.HasIndex(x => x.Number).IsUnique();
        order.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
        order.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        order.Property(x => x.Version).IsConcurrencyToken();
        order.Property(x => x.PartyName).HasMaxLength(160);
        order.Property(x => x.Notes).HasMaxLength(500);
        order.Property(x => x.CreatedBy).HasMaxLength(450);
        order.Property(x => x.CreatedAtUtc).HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
        order.HasIndex(x => new { x.Kind, x.Status, x.CreatedAtUtc, x.Id });
        order.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        order.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        var line = model.Entity<OrderLine>();
        line.ToTable("OrderLines", t =>
        {
            t.HasCheckConstraint("CK_OrderLines_Quantity", "[Quantity] > 0 AND [ProcessedQuantity] >= 0 AND [ProcessedQuantity] <= [Quantity]");
            t.HasCheckConstraint("CK_OrderLines_Price", "[UnitPrice] >= 0");
        });
        line.HasKey(x => x.Id);
        line.HasIndex(x => new { x.OrderId, x.ProductId }).IsUnique();
        line.Property(x => x.Sku).HasMaxLength(64);
        line.Property(x => x.ProductName).HasMaxLength(160);
        line.Property(x => x.UnitPrice).HasPrecision(18, 3);
        line.HasOne(x => x.Order).WithMany(x => x.Lines).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        line.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<StockMovement>().HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
