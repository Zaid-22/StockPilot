using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Application.Catalog;
using StockPilot.Application.Orders;
using StockPilot.Domain.Orders;
using StockPilot.Domain.Security;

namespace StockPilot.Api.Controllers;

[ApiController, Route("api/orders/sales")]
public sealed class SalesOrdersController(IOrderService orders) : ControllerBase
{
    private const string Editors = Roles.Admin + "," + Roles.Staff;
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]
    public Task<PageResult<OrderSummaryDto>> List([FromQuery] OrderQuery query, CancellationToken ct) => orders.ListAsync(OrderKind.Sale, query, ct);
    [HttpGet("{id:guid}")]
    public Task<OrderDto> Get(Guid id, CancellationToken ct) => orders.GetAsync(OrderKind.Sale, id, ct);
    [HttpPost, Authorize(Roles = Editors)]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderInput input, CancellationToken ct)
    {
        var order = await orders.CreateAsync(OrderKind.Sale, input, ActorId, ct);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }
    [HttpPost("{id:guid}/{command:regex(^(confirm|cancel)$)}"), Authorize(Roles = Editors)]
    public Task<OrderDto> Transition(Guid id, string command, OrderActionInput input, CancellationToken ct) => orders.TransitionAsync(OrderKind.Sale, id, command, input, ActorId, ct);
    [HttpPost("{id:guid}/ship"), Authorize(Roles = Editors)]
    public Task<OrderDto> Ship(Guid id, FulfillmentInput input, CancellationToken ct) => orders.FulfillAsync(OrderKind.Sale, id, input, ActorId, ct);
}
