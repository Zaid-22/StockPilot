using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockPilot.Application.Orders;
using StockPilot.Application.Reports;
using StockPilot.Domain.Inventory;
using StockPilot.Infrastructure.Data;

namespace StockPilot.IntegrationTests;

public sealed class ReportTests : WorkflowFixture
{
    [Fact]
    public async Task Empty_reports_have_zero_totals_and_a_complete_daily_series()
    {
        var report = await Client.GetFromJsonAsync<SummaryDto>("/api/reports/summary?from=2026-01-01&to=2026-01-03");
        Assert.Equal(0, report!.ActiveProducts); Assert.Equal(0, report.UnitsOnHand);
        Assert.Equal(0, report.ReceivedOrderValue); Assert.Equal(0, report.ShippedOrderValue);
        Assert.Equal(3, report.DailyStock.Count);
    }

    [Fact]
    public async Task Reports_use_received_and_shipped_quantities_at_order_prices()
    {
        var product = await Product();
        var supplier = await Contact("suppliers"); var customer = await Contact("customers");
        async Task Process(string kind, Guid party, int quantity, int processed, decimal price)
        {
            var draft = await Post<OrderDto>("/api/orders/" + kind, new CreateOrderInput { RequestId = Guid.NewGuid(), PartyId = party,
                Lines = [new() { ProductId = product.Id, Quantity = quantity, UnitPrice = price }] });
            var order = await Post<OrderDto>($"/api/orders/{kind}/{draft.Id}/confirm", new OrderActionInput { RequestId = Guid.NewGuid(), Version = draft.Version });
            await Post<OrderDto>($"/api/orders/{kind}/{order.Id}/" + (kind == "purchases" ? "receive" : "ship"), new FulfillmentInput
            { RequestId = Guid.NewGuid(), Version = order.Version, Reference = "Report fixture", Lines = [new() { LineId = order.Lines[0].Id, Quantity = processed }] });
        }
        await Process("purchases", supplier.Id, 10, 5, 4.125m);
        await Process("sales", customer.Id, 4, 2, 12.125m);
        var report = (await Client.GetFromJsonAsync<SummaryDto>("/api/reports/summary"))!;
        Assert.Equal(3, report.UnitsOnHand); Assert.Equal(1, report.LowStockProducts);
        Assert.Equal(1, report.OpenPurchaseOrders); Assert.Equal(1, report.OpenSalesOrders);
        Assert.Equal(5, report.UnitsReceived); Assert.Equal(2, report.UnitsShipped);
        Assert.Equal(20.625m, report.ReceivedOrderValue); Assert.Equal(24.250m, report.ShippedOrderValue);
        Assert.Equal(5, report.DailyStock.Sum(x => x.Received));
        var historical = (await Client.GetFromJsonAsync<SummaryDto>("/api/reports/summary?from=2000-01-01&to=2000-01-01"))!;
        Assert.Equal(0, historical.UnitsReceived); Assert.Equal(0, historical.ShippedOrderValue);
        Assert.Equal(3, historical.UnitsOnHand); // Current balances are independent of the activity range.
    }

    [Theory]
    [InlineData("from=2026-02-01&to=2026-01-01")]
    [InlineData("from=2020-01-01&to=2026-01-01")]
    [InlineData("from=invalid")]
    [InlineData("to=9999-12-31")]
    public async Task Invalid_date_ranges_are_rejected(string query) =>
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/reports/summary?" + query)).StatusCode);

    [Fact]
    public async Task Reports_require_a_session_but_are_available_to_viewers()
    {
        using var anonymous = App.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/reports/summary")).StatusCode);
        using var viewer = App.Browser(); await Login(viewer, "viewer");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/reports/summary")).StatusCode);
    }
}
