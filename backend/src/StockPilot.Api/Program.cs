using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StockPilot.Infrastructure;
using StockPilot.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);
var development = builder.Environment.IsDevelopment();
var testing = builder.Environment.IsEnvironment("Testing");
var connection = builder.Configuration.GetConnectionString("StockPilot")
    ?? (testing ? "Server=localhost;Database=StockPilotTests;Integrated Security=true" : null)
    ?? throw new InvalidOperationException("Set ConnectionStrings:StockPilot using user-secrets or environment variables.");
builder.Services.AddInfrastructure(connection);
builder.Services.AddControllers(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddOpenApi();
builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(
    new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "StockPilot.Csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = development || testing ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "StockPilot.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = development || testing ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
    options.Events.OnRedirectToLogin = context => WriteAuthProblem(context, 401, "Authentication required");
    options.Events.OnRedirectToAccessDenied = context => WriteAuthProblem(context, 403, "Access denied");
});
var keyPath = builder.Configuration["DataProtection:KeyPath"]
    ?? (development ? Path.Combine(builder.Environment.ContentRootPath, ".keys") : null);
var protection = builder.Services.AddDataProtection().SetApplicationName("StockPilot");
if (!testing)
{
    if (string.IsNullOrWhiteSpace(keyPath))
        throw new InvalidOperationException("Set DataProtection:KeyPath to a persistent, protected directory.");
    protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
}
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
if (!development && !testing)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapGet("/api/health/live", () => Results.Ok(new { status = "ok", application = "StockPilot" })).AllowAnonymous();
app.MapGet("/api/health/ready", async (ApplicationDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.Problem(statusCode: 503, title: "Database unavailable")).AllowAnonymous();
if (development) app.MapOpenApi().AllowAnonymous();
// Unknown API routes must never receive the SPA HTML shell.
app.Map("/api/{**path}", () => Results.Problem(statusCode: 404, title: "Endpoint not found")).AllowAnonymous();
app.MapFallbackToFile("index.html").AllowAnonymous();
if (args.Contains("--initialize"))
{
    await DatabaseInitializer.InitializeAsync(app.Services,
        builder.Configuration["Bootstrap:AdminEmail"] ?? "",
        builder.Configuration["Bootstrap:AdminPassword"] ?? "");
    return;
}
app.Run();

static Task WriteAuthProblem(RedirectContext<CookieAuthenticationOptions> context, int status, string title)
{
    context.Response.StatusCode = status;
    return context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title });
}

public partial class Program;
