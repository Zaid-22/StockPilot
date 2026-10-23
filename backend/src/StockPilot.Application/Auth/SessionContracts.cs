using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace StockPilot.Application.Auth;

public sealed record LoginRequest(
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Required, MaxLength(256)] string Password);

public sealed record SessionUser(string Id, string Email, string DisplayName, IReadOnlyList<string> Roles);

public interface ISessionService
{
    Task<SessionUser?> LoginAsync(LoginRequest request);
    Task<SessionUser?> GetCurrentAsync(ClaimsPrincipal principal);
    Task LogoutAsync();
}

public interface IUserDirectory
{
    Task<IReadOnlyList<SessionUser>> ListAsync(CancellationToken cancellationToken);
}
