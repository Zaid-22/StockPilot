using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockPilot.Application.Auth;
using StockPilot.Application.Catalog;
using StockPilot.Infrastructure.Catalog;
using StockPilot.Infrastructure.Data;
using StockPilot.Infrastructure.Identity;
using StockPilot.Application.Inventory;
using StockPilot.Infrastructure.Inventory;
using StockPilot.Application.Orders;
using StockPilot.Infrastructure.Orders;

namespace StockPilot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<OperationExecutor>();
        services.AddScoped<StockWriter>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IOrderService, OrderService>();
        return services;
    }
}
