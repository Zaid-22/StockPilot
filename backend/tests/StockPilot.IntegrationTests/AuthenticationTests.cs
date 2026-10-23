using System.Net;
using System.Net.Http.Json;
using StockPilot.Application.Auth;
using StockPilot.Domain.Security;

namespace StockPilot.IntegrationTests;

public sealed class AuthenticationTests : IAsyncLifetime
{
    // Each test owns its users, cookies, clock and rate-limit window.
    private readonly StockPilotFactory app = new();
    public Task InitializeAsync() => app.InitializeAsync();
    public Task DisposeAsync() => ((IAsyncLifetime)app).DisposeAsync();

    private static async Task AddCsrfAsync(HttpClient client)
    {
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf!.Token);
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string role = Roles.Admin,
        string password = StockPilotFactory.TestPassword)
    {
        await AddCsrfAsync(client);
        return await client.PostAsJsonAsync("/api/auth/login", new { email = role.ToLowerInvariant() + "@stockpilot.test", password });
    }

    [Fact]
    public async Task Anonymous_requests_receive_401_without_redirect()
    {
        using var client = app.Browser();
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Login_requires_antiforgery_token()
    {
        using var client = app.Browser();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@stockpilot.test", password = StockPilotFactory.TestPassword });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_input_returns_validation_problem()
    {
        using var client = app.Browser();
        await AddCsrfAsync(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "invalid", password = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_password_does_not_create_a_session()
    {
        using var client = app.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, password: "incorrect")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.Viewer)]
    public async Task Login_sets_http_only_cookie_and_session_survives_next_request(string role)
    {
        using var client = app.Browser();
        var response = await LoginAsync(client, role);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.Contains("StockPilot.Session=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        var user = await client.GetFromJsonAsync<SessionUser>("/api/auth/me");
        Assert.Contains(role, user!.Roles);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
    }

    [Theory]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.Viewer)]
    public async Task Non_admin_cannot_read_user_directory(string role)
    {
        using var client = app.Browser();
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, role)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task Admin_can_read_user_directory()
    {
        using var client = app.Browser();
        await LoginAsync(client);
        var users = await client.GetFromJsonAsync<SessionUser[]>("/api/admin/users");
        Assert.Contains(users!, u => u.Roles.Contains(Roles.Viewer));
    }

    [Fact]
    public async Task Logout_requires_fresh_csrf_and_removes_browser_session()
    {
        using var client = app.Browser();
        await LoginAsync(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        await AddCsrfAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Expired_session_is_rejected()
    {
        using var client = app.Browser();
        await LoginAsync(client);
        try
        {
            app.Clock.Advance(TimeSpan.FromHours(9));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        }
        finally { app.Clock.Reset(); }
    }

    [Fact]
    public async Task Five_failed_attempts_lock_the_account()
    {
        using var client = app.Browser();
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, "lockout", "incorrect")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, "lockout")).StatusCode);
    }

    [Fact]
    public async Task Login_rate_limit_rejects_excess_attempts()
    {
        using var client = app.Browser();
        await AddCsrfAsync(client);
        for (var i = 0; i < 20; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login",
                new { email = "unknown@stockpilot.test", password = "incorrect" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        var limited = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "unknown@stockpilot.test", password = "incorrect" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task Csrf_token_from_another_browser_is_rejected()
    {
        using var first = app.Browser();
        using var second = app.Browser();
        var token = await first.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        second.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!.Token);
        var response = await second.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@stockpilot.test", password = StockPilotFactory.TestPassword });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_api_route_is_404_not_spa_html()
    {
        using var client = app.Browser();
        var response = await client.GetAsync("/api/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("json", response.Content.Headers.ContentType!.MediaType!);
    }

    private sealed record CsrfResponse(string Token);
}
