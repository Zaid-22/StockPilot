using Microsoft.EntityFrameworkCore;
using StockPilot.Domain.Catalog;
using StockPilot.Domain.Inventory;

namespace StockPilot.Infrastructure.Inventory;

public static class InventoryConfiguration
{
    public static void ConfigureInventory(this ModelBuilder model)
    {
        model.Entity<InventoryBalance>().Property(x => x.Version).IsConcurrencyToken();
        var operation = model.Entity<StockOperation>();
        operation.ToTable("StockOperations");
        operation.HasKey(x => x.Id);
        operation.Property(x => x.Fingerprint).HasMaxLength(64);
        operation.Property(x => x.ActorId).HasMaxLength(450);
        var movement = model.Entity<StockMovement>();
        movement.ToTable("StockMovements", t =>
        {
            t.HasCheckConstraint("CK_Movements_Nonzero", "[QuantityChange] <> 0");
            t.HasCheckConstraint("CK_Movements_Nonnegative", "[QuantityAfter] >= 0");
        });
        movement.HasKey(x => x.Id);
        movement.Property(x => x.Kind).HasMaxLength(32);
        movement.Property(x => x.Reason).HasMaxLength(500);
        movement.Property(x => x.ActorId).HasMaxLength(450);
        movement.HasIndex(x => new { x.ProductId, x.CreatedAtUtc, x.Id });
        movement.HasIndex(x => new { x.CreatedAtUtc, x.Id });
        movement.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        movement.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        movement.HasOne<StockOperation>().WithMany().HasForeignKey(x => x.OperationId).OnDelete(DeleteBehavior.Restrict);
    }
}
