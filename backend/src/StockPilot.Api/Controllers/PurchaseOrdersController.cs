using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Application.Catalog;
using StockPilot.Application.Orders;
using StockPilot.Domain.Orders;
using StockPilot.Domain.Security;

namespace StockPilot.Api.Controllers;

[ApiController, Route("api/orders/purchases")]
public sealed class PurchaseOrdersController(IOrderService orders) : ControllerBase
{
    private const string Editors = Roles.Admin + "," + Roles.Staff;
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]
    public Task<PageResult<OrderSummaryDto>> List([FromQuery] OrderQuery query, CancellationToken ct) => orders.ListAsync(OrderKind.Purchase, query, ct);
    [HttpGet("{id:guid}")]
    public Task<OrderDto> Get(Guid id, CancellationToken ct) => orders.GetAsync(OrderKind.Purchase, id, ct);
    [HttpPost, Authorize(Roles = Editors)]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderInput input, CancellationToken ct)
    {
        var order = await orders.CreateAsync(OrderKind.Purchase, input, ActorId, ct);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }
    [HttpPost("{id:guid}/{command:regex(^(confirm|cancel)$)}"), Authorize(Roles = Editors)]
    public Task<OrderDto> Transition(Guid id, string command, OrderActionInput input, CancellationToken ct) => orders.TransitionAsync(OrderKind.Purchase, id, command, input, ActorId, ct);
    [HttpPost("{id:guid}/receive"), Authorize(Roles = Editors)]
    public Task<OrderDto> Receive(Guid id, FulfillmentInput input, CancellationToken ct) => orders.FulfillAsync(OrderKind.Purchase, id, input, ActorId, ct);
}
