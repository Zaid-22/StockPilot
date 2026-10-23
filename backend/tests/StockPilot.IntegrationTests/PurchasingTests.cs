using System.Net;
using System.Net.Http.Json;
using StockPilot.Application.Catalog;
using StockPilot.Application.Inventory;
using StockPilot.Application.Orders;

namespace StockPilot.IntegrationTests;

public sealed class PurchasingTests : WorkflowFixture
{
    private const string Path = "/api/orders/purchases";
    private async Task<OrderDto> Draft(Guid productId, int quantity = 10)
    {
        var supplier = await Contact("suppliers");
        return await Post<OrderDto>(Path, new CreateOrderInput { RequestId = Guid.NewGuid(), PartyId = supplier.Id,
            Lines = [new() { ProductId = productId, Quantity = quantity, UnitPrice = 4.125m }] });
    }
    private Task<OrderDto> Confirm(OrderDto order) => Post<OrderDto>($"{Path}/{order.Id}/confirm", new OrderActionInput { RequestId = Guid.NewGuid(), Version = order.Version });
    private static FulfillmentInput Receipt(OrderDto order, int quantity) => new() { RequestId = Guid.NewGuid(), Version = order.Version,
        Reference = "Delivery 123", Lines = [new() { LineId = order.Lines[0].Id, Quantity = quantity }] };

    [Fact]
    public async Task Purchase_can_be_partially_received_and_retried_without_double_stock()
    {
        var product = await Product(); var draft = await Draft(product.Id);
        Assert.Equal("Draft", draft.Status); Assert.Equal(41.250m, draft.Total);
        Assert.Equal(0, (await Balance(product.Id)).QuantityOnHand);
        var confirmed = await Confirm(draft);
        var receipt = Receipt(confirmed, 4);
        var partial = await Post<OrderDto>($"{Path}/{draft.Id}/receive", receipt);
        Assert.Equal("Partial", partial.Status); Assert.Equal(4, partial.Lines[0].ProcessedQuantity);
        var retry = await Post<OrderDto>($"{Path}/{draft.Id}/receive", receipt);
        Assert.Equal(partial.Version, retry.Version);
        Assert.Equal(4, (await Balance(product.Id)).QuantityOnHand);
        var completed = await Post<OrderDto>($"{Path}/{draft.Id}/receive", Receipt(partial, 6));
        Assert.Equal("Completed", completed.Status); Assert.Equal(10, (await Balance(product.Id)).QuantityOnHand);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"{Path}/{draft.Id}/receive", Receipt(completed, 1))).StatusCode);
        var journal = await Client.GetFromJsonAsync<PageResult<MovementDto>>("/api/inventory/movements");
        Assert.Equal(2, journal!.TotalCount);
        Assert.All(journal.Items, x => { Assert.Equal("Receipt", x.Kind); Assert.Equal(draft.Id, x.OrderId); });
    }

    [Fact]
    public async Task Draft_receipt_and_excess_receipt_are_rejected()
    {
        var product = await Product(); var draft = await Draft(product.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"{Path}/{draft.Id}/receive", Receipt(draft, 1))).StatusCode);
        var confirmed = await Confirm(draft);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"{Path}/{draft.Id}/receive", Receipt(confirmed, 11))).StatusCode);
        Assert.Equal(0, (await Balance(product.Id)).QuantityOnHand);
    }

    [Fact]
    public async Task Creation_is_idempotent_and_duplicate_product_lines_are_invalid()
    {
        var product = await Product(); var supplier = await Contact("suppliers");
        var input = new CreateOrderInput { RequestId = Guid.NewGuid(), PartyId = supplier.Id,
            Lines = [new() { ProductId = product.Id, Quantity = 2, UnitPrice = 1 }] };
        var first = await Post<OrderDto>(Path, input); var retry = await Post<OrderDto>(Path, input);
        Assert.Equal(first.Id, retry.Id);
        input.RequestId = Guid.NewGuid(); input.Lines.Add(input.Lines[0]);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync(Path, input)).StatusCode);
        Assert.Equal(1, (await Client.GetFromJsonAsync<PageResult<OrderSummaryDto>>(Path))!.TotalCount);
    }

    [Fact]
    public async Task Stale_versions_and_cancelling_after_receiving_are_rejected()
    {
        var product = await Product(); var draft = await Draft(product.Id); var confirmed = await Confirm(draft);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"{Path}/{draft.Id}/cancel", new OrderActionInput { RequestId = Guid.NewGuid(), Version = draft.Version })).StatusCode);
        var partial = await Post<OrderDto>($"{Path}/{draft.Id}/receive", Receipt(confirmed, 1));
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"{Path}/{draft.Id}/cancel", new OrderActionInput { RequestId = Guid.NewGuid(), Version = partial.Version })).StatusCode);
        var second = await Draft(product.Id);
        var cancelled = await Post<OrderDto>($"{Path}/{second.Id}/cancel", new OrderActionInput { RequestId = Guid.NewGuid(), Version = second.Version });
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"{Path}/{second.Id}/confirm", new OrderActionInput { RequestId = Guid.NewGuid(), Version = cancelled.Version })).StatusCode);
    }

    [Fact]
    public async Task Invalid_later_line_rolls_back_the_entire_receipt()
    {
        var first = await Product(); var second = await Product(); var supplier = await Contact("suppliers");
        var draft = await Post<OrderDto>(Path, new CreateOrderInput { RequestId = Guid.NewGuid(), PartyId = supplier.Id,
            Lines = [new() { ProductId = first.Id, Quantity = 5 }, new() { ProductId = second.Id, Quantity = 5 }] });
        var confirmed = await Confirm(draft);
        var sorted = confirmed.Lines.OrderBy(x => x.ProductId).ToArray();
        var bad = new FulfillmentInput { RequestId = Guid.NewGuid(), Version = confirmed.Version, Reference = "Broken receipt",
            Lines = [new() { LineId = sorted[0].Id, Quantity = 2 }, new() { LineId = sorted[1].Id, Quantity = 6 }] };
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"{Path}/{draft.Id}/receive", bad)).StatusCode);
        Assert.Equal(0, (await Balance(first.Id)).QuantityOnHand); Assert.Equal(0, (await Balance(second.Id)).QuantityOnHand);
        Assert.Empty((await Client.GetFromJsonAsync<PageResult<MovementDto>>("/api/inventory/movements"))!.Items);
        Assert.Equal(confirmed.Version, (await Client.GetFromJsonAsync<OrderDto>($"{Path}/{draft.Id}"))!.Version);
    }

    [Fact]
    public async Task Viewer_cannot_create_confirm_cancel_or_receive_orders()
    {
        var product = await Product(); var draft = await Draft(product.Id);
        using var viewer = App.Browser(); await Login(viewer, "viewer");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(Path, new { })).StatusCode);
        foreach (var command in new[] { "confirm", "cancel", "receive" })
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"{Path}/{draft.Id}/{command}", new { })).StatusCode);
    }
}
