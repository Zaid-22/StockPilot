using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Application.Catalog;
using StockPilot.Application.Inventory;
using StockPilot.Domain.Security;

namespace StockPilot.Api.Controllers;

[ApiController, Route("api/inventory")]
public sealed class InventoryController(IInventoryService inventory) : ControllerBase
{
    [HttpGet("balances")]
    public Task<PageResult<BalanceDto>> Balances([FromQuery] StockQuery query, CancellationToken ct) => inventory.BalancesAsync(query, ct);
    [HttpGet("movements")]
    public Task<PageResult<MovementDto>> Movements([FromQuery] StockQuery query, CancellationToken ct) => inventory.MovementsAsync(query, ct);
    [HttpPost("adjustments"), Authorize(Roles = Roles.Admin + "," + Roles.Staff)]
    public Task<AdjustmentResult> Adjust(AdjustmentInput input, CancellationToken ct) =>
        inventory.AdjustAsync(input, User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct);
}
