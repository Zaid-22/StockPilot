using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StockPilot.Infrastructure.Identity;
using StockPilot.Infrastructure.Catalog;
using StockPilot.Infrastructure.Inventory;
using StockPilot.Domain.Inventory;
using StockPilot.Infrastructure.Orders;

namespace StockPilot.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureCatalog();
        builder.ConfigureInventory();
        builder.ConfigureOrders();
        builder.Entity<ApplicationUser>().Property(u => u.DisplayName).HasMaxLength(100);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardHistory();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardHistory();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void GuardHistory()
    {
        if (ChangeTracker.Entries().Any(x => (x.Entity is StockMovement or StockOperation)
            && x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Stock history is append-only. Post a correcting movement instead.");
    }
}
