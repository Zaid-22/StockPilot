using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockPilot.Application.Catalog;
using StockPilot.Application.Inventory;
using StockPilot.Domain.Catalog;
using StockPilot.Domain.Inventory;
using StockPilot.Infrastructure.Data;

namespace StockPilot.IntegrationTests;

public sealed class InventoryTests : WorkflowFixture
{
    [Fact]
    public async Task Adjustment_retry_returns_original_result_and_posts_one_movement()
    {
        var product = await Product();
        var input = new AdjustmentInput { ProductId = product.Id, Version = (await Balance(product.Id)).Version,
            QuantityChange = 8, Reason = "Counted shelf", RequestId = Guid.NewGuid() };
        var first = await Post<AdjustmentResult>("/api/inventory/adjustments", input);
        var retry = await Post<AdjustmentResult>("/api/inventory/adjustments", input);
        Assert.Equal(first, retry);
        Assert.Equal(8, (await Balance(product.Id)).QuantityOnHand);
        var journal = await Client.GetFromJsonAsync<PageResult<MovementDto>>("/api/inventory/movements?productId=" + product.Id);
        var movement = Assert.Single(journal!.Items);
        Assert.Equal(8, movement.QuantityChange);
        Assert.Equal("Counted shelf", movement.Reason);
        Assert.NotEmpty(movement.ActorId);
        Assert.Equal(DateTimeKind.Utc, movement.CreatedAtUtc.Kind);
        input.QuantityChange = 9;
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync("/api/inventory/adjustments", input)).StatusCode);
    }

    [Fact]
    public async Task Stale_and_negative_adjustments_leave_stock_and_journal_unchanged()
    {
        var product = await Product();
        var stale = (await Balance(product.Id)).Version;
        await AddStock(product.Id, 2);
        var input = new AdjustmentInput { ProductId = product.Id, Version = stale, QuantityChange = 1, Reason = "Stale", RequestId = Guid.NewGuid() };
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync("/api/inventory/adjustments", input)).StatusCode);
        input.Version = (await Balance(product.Id)).Version; input.QuantityChange = -3;
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync("/api/inventory/adjustments", input)).StatusCode);
        Assert.Equal(2, (await Balance(product.Id)).QuantityOnHand);
        Assert.Single((await Client.GetFromJsonAsync<PageResult<MovementDto>>("/api/inventory/movements"))!.Items);
    }

    [Fact]
    public async Task Concurrent_contexts_cannot_overwrite_the_same_balance()
    {
        var product = await Product();
        using var firstScope = App.Services.CreateScope();
        using var secondScope = App.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var a = await first.Set<InventoryBalance>().SingleAsync(x => x.ProductId == product.Id);
        var b = await second.Set<InventoryBalance>().SingleAsync(x => x.ProductId == product.Id);
        a.QuantityOnHand = 1; a.Version = Guid.NewGuid();
        b.QuantityOnHand = 2; b.Version = Guid.NewGuid();
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        Assert.Equal(1, (await Balance(product.Id)).QuantityOnHand);
    }

    [Fact]
    public async Task Journal_cannot_be_edited_through_the_application_context()
    {
        var product = await Product(); await AddStock(product.Id, 2);
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var movement = await db.Set<StockMovement>().SingleAsync();
        movement.Reason = "Rewritten";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public async Task Stock_writes_require_role_csrf_and_valid_quantities()
    {
        var product = await Product();
        var input = new AdjustmentInput { ProductId = product.Id, Version = (await Balance(product.Id)).Version, QuantityChange = 1, Reason = "Count", RequestId = Guid.NewGuid() };
        using var viewer = App.Browser(); await Login(viewer, "viewer");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/inventory/balances")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/inventory/adjustments", input)).StatusCode);
        Client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/inventory/adjustments", input)).StatusCode);
    }

    [Fact]
    public async Task Zero_adjustments_are_rejected_and_low_stock_filter_works()
    {
        var product = await Product();
        var input = new AdjustmentInput { ProductId = product.Id, Version = (await Balance(product.Id)).Version, QuantityChange = 0, Reason = "Count", RequestId = Guid.NewGuid() };
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/inventory/adjustments", input)).StatusCode);
        Assert.Single((await Client.GetFromJsonAsync<PageResult<BalanceDto>>("/api/inventory/balances?lowStockOnly=true"))!.Items);
        await AddStock(product.Id, 4);
        Assert.Empty((await Client.GetFromJsonAsync<PageResult<BalanceDto>>("/api/inventory/balances?lowStockOnly=true"))!.Items);
    }
}
