using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StockPilot.Infrastructure.Identity;
using StockPilot.Infrastructure.Catalog;

namespace StockPilot.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureCatalog();
        builder.Entity<ApplicationUser>().Property(u => u.DisplayName).HasMaxLength(100);
    }
}
