using System.Net;
using System.Net.Http.Json;
using StockPilot.Application.Catalog;
using StockPilot.Application.Inventory;
using StockPilot.Application.Orders;

namespace StockPilot.IntegrationTests;

public sealed class SalesTests : WorkflowFixture
{
    private const string Path = "/api/orders/sales";
    private async Task<OrderDto> Confirmed(params (Guid ProductId, int Quantity)[] products)
    {
        var customer = await Contact("customers");
        var order = await Post<OrderDto>(Path, new CreateOrderInput { RequestId = Guid.NewGuid(), PartyId = customer.Id,
            Lines = products.Select(x => new OrderLineInput { ProductId = x.ProductId, Quantity = x.Quantity, UnitPrice = 12.125m }).ToList() });
        return await Post<OrderDto>($"{Path}/{order.Id}/confirm", new OrderActionInput { RequestId = Guid.NewGuid(), Version = order.Version });
    }
    private static FulfillmentInput Shipment(OrderDto order, int quantity) => new() { RequestId = Guid.NewGuid(), Version = order.Version,
        Reference = "Dispatch 123", Lines = [new() { LineId = order.Lines[0].Id, Quantity = quantity }] };

    [Fact]
    public async Task Sales_confirmation_does_not_reserve_stock_and_shipping_is_idempotent()
    {
        var product = await Product(); await AddStock(product.Id, 5);
        var order = await Confirmed((product.Id, 5));
        Assert.Equal(5, (await Balance(product.Id)).QuantityOnHand);
        var input = Shipment(order, 2);
        var partial = await Post<OrderDto>($"{Path}/{order.Id}/ship", input);
        Assert.Equal("Partial", partial.Status);
        Assert.Equal(3, (await Balance(product.Id)).QuantityOnHand);
        var retry = await Post<OrderDto>($"{Path}/{order.Id}/ship", input);
        Assert.Equal(partial.Version, retry.Version);
        Assert.Equal(3, (await Balance(product.Id)).QuantityOnHand);
        var complete = await Post<OrderDto>($"{Path}/{order.Id}/ship", Shipment(partial, 3));
        Assert.Equal("Completed", complete.Status); Assert.Equal(0, (await Balance(product.Id)).QuantityOnHand);
        var movements = await Client.GetFromJsonAsync<PageResult<MovementDto>>("/api/inventory/movements");
        Assert.Equal(-5, movements!.Items.Where(x => x.Kind == "Shipment").Sum(x => x.QuantityChange));
    }

    [Fact]
    public async Task Competing_orders_cannot_consume_the_same_last_unit()
    {
        var product = await Product(); await AddStock(product.Id, 1);
        var first = await Confirmed((product.Id, 1)); var second = await Confirmed((product.Id, 1));
        await Post<OrderDto>($"{Path}/{first.Id}/ship", Shipment(first, 1));
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"{Path}/{second.Id}/ship", Shipment(second, 1))).StatusCode);
        Assert.Equal(0, (await Balance(product.Id)).QuantityOnHand);
        Assert.Equal("Confirmed", (await Client.GetFromJsonAsync<OrderDto>($"{Path}/{second.Id}"))!.Status);
    }

    [Fact]
    public async Task Insufficient_stock_on_one_line_rolls_back_the_whole_shipment()
    {
        var first = await Product(); var second = await Product();
        var ids = new[] { first.Id, second.Id }.Order().ToArray();
        await AddStock(ids[0], 5);
        var order = await Confirmed((ids[0], 2), (ids[1], 2));
        var input = new FulfillmentInput { RequestId = Guid.NewGuid(), Version = order.Version, Reference = "Batch",
            Lines = order.Lines.Select(x => new FulfillmentLineInput { LineId = x.Id, Quantity = 2 }).ToList() };
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"{Path}/{order.Id}/ship", input)).StatusCode);
        Assert.Equal(5, (await Balance(ids[0])).QuantityOnHand);
        Assert.Equal(0, (await Balance(ids[1])).QuantityOnHand);
        Assert.All((await Client.GetFromJsonAsync<OrderDto>($"{Path}/{order.Id}"))!.Lines, x => Assert.Equal(0, x.ProcessedQuantity));
        Assert.DoesNotContain((await Client.GetFromJsonAsync<PageResult<MovementDto>>("/api/inventory/movements"))!.Items, x => x.Kind == "Shipment");
    }

    [Fact]
    public async Task Sales_routes_cannot_process_purchase_orders_and_viewers_cannot_ship()
    {
        var product = await Product(); var supplier = await Contact("suppliers");
        var purchase = await Post<OrderDto>("/api/orders/purchases", new CreateOrderInput { RequestId = Guid.NewGuid(), PartyId = supplier.Id,
            Lines = [new() { ProductId = product.Id, Quantity = 1 }] });
        Assert.Equal(HttpStatusCode.NotFound, (await Client.PostAsJsonAsync($"{Path}/{purchase.Id}/ship", Shipment(purchase, 1))).StatusCode);
        var sale = await Confirmed((product.Id, 1));
        using var viewer = App.Browser(); await Login(viewer, "viewer");
        foreach (var action in new[] { "ship", "confirm", "cancel" })
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"{Path}/{sale.Id}/{action}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(Path, new { })).StatusCode);
    }

    [SqlServerFact]
    public async Task Parallel_sql_server_shipments_allow_only_one_last_unit_consumer()
    {
        var product = await Product(); await AddStock(product.Id, 1);
        var first = await Confirmed((product.Id, 1)); var second = await Confirmed((product.Id, 1));
        var responses = await Task.WhenAll(
            Client.PostAsJsonAsync($"{Path}/{first.Id}/ship", Shipment(first, 1)),
            Client.PostAsJsonAsync($"{Path}/{second.Id}/ship", Shipment(second, 1)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(0, (await Balance(product.Id)).QuantityOnHand);
        Assert.Single((await Client.GetFromJsonAsync<PageResult<MovementDto>>("/api/inventory/movements"))!.Items, x => x.Kind == "Shipment");
    }
}

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION_STRING")))
            Skip = "Requires SQL Server; the SQLite in-memory fixture shares one connection.";
    }
}
