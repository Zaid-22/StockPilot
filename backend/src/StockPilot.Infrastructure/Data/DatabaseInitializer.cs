using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockPilot.Domain.Security;
using StockPilot.Infrastructure.Identity;

namespace StockPilot.Infrastructure.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Set Bootstrap:AdminEmail and Bootstrap:AdminPassword before initialization.");
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
            if (!await roles.RoleExistsAsync(role)) EnsureSuccess(await roles.CreateAsync(new IdentityRole(role)));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!await users.IsInRoleAsync(existing, Roles.Admin))
                throw new InvalidOperationException("Bootstrap email already belongs to a non-admin account. No role was changed.");
            return;
        }
        var admin = new ApplicationUser { UserName = email, Email = email, DisplayName = "Workspace administrator" };
        await using var transaction = await db.Database.BeginTransactionAsync();
        EnsureSuccess(await users.CreateAsync(admin, password));
        EnsureSuccess(await users.AddToRoleAsync(admin, Roles.Admin));
        await transaction.CommitAsync();
    }

    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
