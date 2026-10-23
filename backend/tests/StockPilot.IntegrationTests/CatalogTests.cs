using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockPilot.Application.Catalog;
using StockPilot.Domain.Catalog;
using StockPilot.Infrastructure.Data;

namespace StockPilot.IntegrationTests;

public sealed class CatalogTests : IAsyncLifetime
{
    private readonly StockPilotFactory app = new();
    private HttpClient client = null!;
    public async Task InitializeAsync()
    {
        await app.InitializeAsync();
        client = app.Browser();
        await LoginAsync(client, "staff");
    }
    public async Task DisposeAsync() { client.Dispose(); await ((IAsyncLifetime)app).DisposeAsync(); }
    private static async Task LoginAsync(HttpClient browser, string role)
    {
        var csrf = await browser.GetFromJsonAsync<Token>("/api/auth/csrf");
        browser.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf!.TokenValue);
        var login = await browser.PostAsJsonAsync("/api/auth/login", new { email = role + "@stockpilot.test", password = StockPilotFactory.TestPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        browser.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        csrf = await browser.GetFromJsonAsync<Token>("/api/auth/csrf");
        browser.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf!.TokenValue);
    }
    private async Task<ReferenceDto> Category(string name = "Electronics") =>
        await CreateReference("categories", new ReferenceInput { Name = name });
    private async Task<ReferenceDto> CreateReference(string kind, ReferenceInput input)
    {
        var response = await client.PostAsJsonAsync("/api/catalog/" + kind, input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReferenceDto>())!;
    }
    private static ProductInput Input(Guid category, string sku = "KEY-001", string name = "Keyboard") =>
        new() { CategoryId = category, Sku = sku, Name = name, SellingPrice = 25.125m, ReorderLevel = 3 };
    private async Task<ProductDto> CreateProduct(ProductInput input)
    {
        var response = await client.PostAsJsonAsync("/api/catalog/products", input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        return (await response.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    [Fact]
    public async Task Product_creation_persists_normalized_sku_and_one_zero_balance()
    {
        var category = await Category();
        var product = await CreateProduct(Input(category.Id, " key-001 "));
        Assert.Equal("KEY-001", product.Sku);
        Assert.Equal(0, product.QuantityOnHand);
        var reloaded = await client.GetFromJsonAsync<ProductDto>("/api/catalog/products/" + product.Id);
        Assert.Equal(25.125m, reloaded!.SellingPrice);
        using var scope = app.Services.CreateScope();
        var balances = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<InventoryBalance>().Where(b => b.ProductId == product.Id).ToListAsync();
        Assert.Single(balances);
        Assert.Equal(Warehouse.MainId, balances[0].WarehouseId);
        Assert.Equal(0, balances[0].QuantityOnHand);
    }

    [Fact]
    public async Task Duplicate_sku_is_rejected_even_when_original_is_inactive()
    {
        var category = await Category();
        var input = Input(category.Id); input.IsActive = false;
        await CreateProduct(input);
        var duplicate = await client.PostAsJsonAsync("/api/catalog/products", Input(category.Id, " key-001 "));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var products = await client.GetFromJsonAsync<PageResult<ProductDto>>("/api/catalog/products");
        Assert.Equal(1, products!.TotalCount);
    }

    [Fact]
    public async Task Database_enforces_unique_normalized_sku()
    {
        var category = await Category();
        await CreateProduct(Input(category.Id));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Add(new Product { Sku = "KEY-001", NormalizedSku = "KEY-001", Name = "Duplicate", CategoryId = category.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Editing_with_a_stale_version_does_not_overwrite_newer_data()
    {
        var category = await Category();
        var original = await CreateProduct(Input(category.Id));
        var update = Input(category.Id); update.Version = original.Version; update.Name = "Updated keyboard";
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/catalog/products/" + original.Id, update)).StatusCode);
        update.Name = "Stale edit";
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/catalog/products/" + original.Id, update)).StatusCode);
        Assert.Equal("Updated keyboard", (await client.GetFromJsonAsync<ProductDto>("/api/catalog/products/" + original.Id))!.Name);
    }

    [Fact]
    public async Task Products_can_be_searched_filtered_and_paginated()
    {
        var firstCategory = await Category();
        var otherCategory = await Category("Office");
        await CreateProduct(Input(firstCategory.Id, "K-01", "Alpha keyboard"));
        await CreateProduct(Input(firstCategory.Id, "K-02", "Beta keyboard"));
        var inactive = Input(otherCategory.Id, "M-01", "Mouse"); inactive.IsActive = false;
        await CreateProduct(inactive);
        var page = await client.GetFromJsonAsync<PageResult<ProductDto>>($"/api/catalog/products?q=KEYBOARD&categoryId={firstCategory.Id}&isActive=true&page=2&pageSize=1");
        Assert.Equal(2, page!.TotalCount);
        Assert.Equal("Beta keyboard", Assert.Single(page.Items).Name);
        var search = await client.GetFromJsonAsync<PageResult<ProductDto>>("/api/catalog/products?q=m-01&isActive=false");
        Assert.Equal("Mouse", Assert.Single(search!.Items).Name);
    }

    [Fact]
    public async Task Product_deactivation_preserves_record_and_balance()
    {
        var category = await Category();
        var product = await CreateProduct(Input(category.Id));
        var update = Input(category.Id); update.Version = product.Version; update.IsActive = false;
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/catalog/products/" + product.Id, update)).StatusCode);
        var stored = await client.GetFromJsonAsync<ProductDto>("/api/catalog/products/" + product.Id);
        Assert.False(stored!.IsActive);
        Assert.Equal(0, stored.QuantityOnHand);
        var active = await client.GetFromJsonAsync<PageResult<ProductDto>>("/api/catalog/products?isActive=true");
        Assert.Empty(active!.Items);
    }

    [Theory]
    [InlineData("suppliers")]
    [InlineData("customers")]
    public async Task Contact_records_can_be_created_edited_and_deactivated(string kind)
    {
        var contact = await CreateReference(kind, new ReferenceInput { Name = "Example company", Email = "team@example.test", Phone = "12345" });
        var update = new ReferenceInput { Name = "Renamed company", Email = contact.Email, Address = "Amman", IsActive = false, Version = contact.Version };
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/catalog/{kind}/{contact.Id}", update)).StatusCode);
        var stored = await client.GetFromJsonAsync<ReferenceDto>($"/api/catalog/{kind}/{contact.Id}");
        Assert.Equal("Renamed company", stored!.Name); Assert.False(stored.IsActive); Assert.Equal("Amman", stored.Address);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/catalog/{kind}/{contact.Id}", update)).StatusCode);
    }

    [Fact]
    public async Task Category_names_are_unique_after_trimming_and_normalization()
    {
        await Category();
        var duplicate = await client.PostAsJsonAsync("/api/catalog/categories", new ReferenceInput { Name = " electronics " });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Inactive_category_cannot_be_assigned_to_new_product()
    {
        var category = await CreateReference("categories", new ReferenceInput { Name = "Inactive", IsActive = false });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/catalog/products", Input(category.Id))).StatusCode);
    }

    [Fact]
    public async Task Viewer_can_read_but_cannot_modify_catalog()
    {
        var category = await Category();
        using var viewer = app.Browser(); await LoginAsync(viewer, "viewer");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/catalog/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/catalog/products", Input(category.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PutAsJsonAsync("/api/catalog/categories/" + category.Id, new ReferenceInput { Name = "Changed", Version = category.Version })).StatusCode);
    }

    [Theory]
    [InlineData("/api/catalog/products?page=0")]
    [InlineData("/api/catalog/products?pageSize=101")]
    [InlineData("/api/catalog/products?categoryId=invalid")]
    public async Task Invalid_filters_are_rejected(string path) =>
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path)).StatusCode);

    [Fact]
    public async Task Negative_prices_excess_precision_and_invalid_skus_are_rejected()
    {
        var category = await Category();
        foreach (var input in new[] { Input(category.Id, "bad sku"), Input(category.Id), Input(category.Id) }.Select((value, index) => { if (index == 1) value.SellingPrice = -1; if (index == 2) value.SellingPrice = 1.2345m; return value; }))
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/catalog/products", input)).StatusCode);
    }
    private sealed record Token([property: System.Text.Json.Serialization.JsonPropertyName("token")] string TokenValue);
}
