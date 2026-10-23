using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StockPilot.Application.Auth;

namespace StockPilot.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(ISessionService sessions) : ControllerBase
{
    [HttpGet("csrf"), AllowAnonymous]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery) =>
        Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<ActionResult<SessionUser>> Login(LoginRequest request)
    {
        var user = await sessions.LoginAsync(request);
        return user is null
            ? Problem(statusCode: 401, title: "Sign-in unsuccessful", detail: "Check your email and password, or try again later.")
            : Ok(user);
    }

    [HttpGet("me")]
    public async Task<ActionResult<SessionUser>> Me()
    {
        var user = await sessions.GetCurrentAsync(User);
        return user is null ? Unauthorized() : Ok(user);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await sessions.LogoutAsync();
        return NoContent();
    }
}
