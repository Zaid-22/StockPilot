using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockPilot.Domain.Security;
using StockPilot.Infrastructure.Data;
using StockPilot.Infrastructure.Identity;

namespace StockPilot.IntegrationTests;

public sealed class StockPilotFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestPassword = "Test-only-Passphrase-2026!";
    private readonly SqliteConnection sqlite = new("Data Source=:memory:");
    private readonly string? sqlConnection = Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION_STRING");
    private string? isolatedSqlConnection;
    public TestClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            if (!string.IsNullOrWhiteSpace(sqlConnection))
            {
                var connection = new SqlConnectionStringBuilder(sqlConnection)
                { InitialCatalog = "StockPilotTest_" + Guid.NewGuid().ToString("N") };
                isolatedSqlConnection = connection.ConnectionString;
                services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(isolatedSqlConnection));
            }
            else
            {
                sqlite.Open();
                services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(sqlite));
            }
            services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme,
                options => options.TimeProvider = Clock);
        });
    }

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions
    { AllowAutoRedirect = false, HandleCookies = true, BaseAddress = new Uri("https://localhost") });

    public async Task InitializeAsync()
    {
        using var client = Browser();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (isolatedSqlConnection is not null) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var role in Roles.All)
        {
            Check(await roles.CreateAsync(new IdentityRole(role)));
            var email = role.ToLowerInvariant() + "@stockpilot.test";
            var user = new ApplicationUser { Email = email, UserName = email, DisplayName = role + " tester" };
            Check(await users.CreateAsync(user, TestPassword));
            Check(await users.AddToRoleAsync(user, role));
        }
        var locked = new ApplicationUser { Email = "lockout@stockpilot.test", UserName = "lockout@stockpilot.test" };
        Check(await users.CreateAsync(locked, TestPassword));
    }

    private static void Check(IdentityResult result) => Assert.True(result.Succeeded,
        string.Join("; ", result.Errors.Select(e => e.Description)));

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (isolatedSqlConnection is not null)
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureDeletedAsync();
        }
        await base.DisposeAsync();
        await sqlite.DisposeAsync();
    }
}

public sealed class TestClock : TimeProvider
{
    private TimeSpan offset;
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.Add(offset);
    public void Advance(TimeSpan value) => offset += value;
    public void Reset() => offset = TimeSpan.Zero;
}
