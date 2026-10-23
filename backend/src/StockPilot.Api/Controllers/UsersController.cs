using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Application.Auth;
using StockPilot.Domain.Security;

namespace StockPilot.Api.Controllers;

[ApiController, Route("api/admin/users"), Authorize(Roles = Roles.Admin)]
public sealed class UsersController(IUserDirectory directory) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SessionUser>>> List(CancellationToken cancellationToken) =>
        Ok(await directory.ListAsync(cancellationToken));
}
