using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using StockPilot.Application.Auth;

namespace StockPilot.Infrastructure.Identity;

public sealed class SessionService(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
    : ISessionService
{
    public async Task<SessionUser?> LoginAsync(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null) return null;
        var result = await signIn.PasswordSignInAsync(user, request.Password,
            isPersistent: false, lockoutOnFailure: true);
        return result.Succeeded ? await DescribeAsync(user) : null;
    }

    public async Task<SessionUser?> GetCurrentAsync(ClaimsPrincipal principal)
    {
        var user = await users.GetUserAsync(principal);
        return user is null ? null : await DescribeAsync(user);
    }

    public Task LogoutAsync() => signIn.SignOutAsync();

    private async Task<SessionUser> DescribeAsync(ApplicationUser user) =>
        new(user.Id, user.Email!, user.DisplayName, (await users.GetRolesAsync(user)).ToArray());
}
