using System.Net;
using System.Net.Http.Json;
using StockPilot.Application.Catalog;
using StockPilot.Application.Inventory;

namespace StockPilot.IntegrationTests;

public abstract class WorkflowFixture : IAsyncLifetime
{
    protected readonly StockPilotFactory App = new();
    protected HttpClient Client = null!;
    public async Task InitializeAsync()
    {
        await App.InitializeAsync();
        Client = App.Browser();
        await Login(Client, "staff");
    }
    public async Task DisposeAsync() { Client.Dispose(); await ((IAsyncLifetime)App).DisposeAsync(); }
    protected static async Task Login(HttpClient browser, string role)
    {
        var token = await browser.GetFromJsonAsync<Token>("/api/auth/csrf");
        browser.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!.Value);
        var response = await browser.PostAsJsonAsync("/api/auth/login", new { email = role + "@stockpilot.test", password = StockPilotFactory.TestPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        browser.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        token = await browser.GetFromJsonAsync<Token>("/api/auth/csrf");
        browser.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!.Value);
    }
    protected async Task<T> Post<T>(string path, object input)
    {
        var response = await Client.PostAsJsonAsync(path, input);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    protected Task<ReferenceDto> Contact(string kind) => Post<ReferenceDto>("/api/catalog/" + kind, new { requestId = Guid.NewGuid(), name = "Test " + Guid.NewGuid() });
    protected async Task<ProductDto> Product()
    {
        var category = await Contact("categories");
        return await Post<ProductDto>("/api/catalog/products", new { requestId = Guid.NewGuid(), name = "Test product", sku = "SKU-" + Guid.NewGuid().ToString("N"), categoryId = category.Id, sellingPrice = 12.125m, reorderLevel = 3 });
    }
    protected async Task<BalanceDto> Balance(Guid productId) => Assert.Single((await Client.GetFromJsonAsync<PageResult<BalanceDto>>("/api/inventory/balances?productId=" + productId))!.Items);
    protected async Task<AdjustmentResult> AddStock(Guid productId, int quantity) => await Post<AdjustmentResult>("/api/inventory/adjustments", new AdjustmentInput
    { ProductId = productId, QuantityChange = quantity, Reason = "Opening stock", RequestId = Guid.NewGuid(), Version = (await Balance(productId)).Version });
    private sealed record Token([property: System.Text.Json.Serialization.JsonPropertyName("token")] string Value);
}
