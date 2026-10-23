using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Application.Catalog;
using StockPilot.Domain.Catalog;
using StockPilot.Domain.Security;

namespace StockPilot.Api.Controllers;

[ApiController, Route("api/catalog")]
public sealed class CatalogController(ICatalogService catalog) : ControllerBase
{
    private const string Editors = Roles.Admin + "," + Roles.Staff;
    [HttpGet("products")]
    public Task<PageResult<ProductDto>> Products([FromQuery] CatalogQuery query, CancellationToken ct) => catalog.ProductsAsync(query, ct);
    [HttpGet("products/{id:guid}")]
    public Task<ProductDto> Product(Guid id, CancellationToken ct) => catalog.ProductAsync(id, ct);
    [HttpPost("products"), Authorize(Roles = Editors)]
    public async Task<ActionResult<ProductDto>> CreateProduct(ProductInput input, CancellationToken ct)
    {
        var product = await catalog.SaveProductAsync(null, input, ct);
        return CreatedAtAction(nameof(Product), new { id = product.Id }, product);
    }
    [HttpPut("products/{id:guid}"), Authorize(Roles = Editors)]
    public Task<ProductDto> UpdateProduct(Guid id, ProductInput input, CancellationToken ct) => catalog.SaveProductAsync(id, input, ct);

    [HttpGet("{kind:regex(^(categories|suppliers|customers)$)}")]
    public Task<PageResult<ReferenceDto>> References(string kind, [FromQuery] CatalogQuery query, CancellationToken ct) => catalog.ReferencesAsync(Kind(kind), query, ct);
    [HttpGet("{kind:regex(^(categories|suppliers|customers)$)}/{id:guid}")]
    public Task<ReferenceDto> Reference(string kind, Guid id, CancellationToken ct) => catalog.ReferenceAsync(Kind(kind), id, ct);
    [HttpPost("{kind:regex(^(categories|suppliers|customers)$)}"), Authorize(Roles = Editors)]
    public async Task<ActionResult<ReferenceDto>> CreateReference(string kind, ReferenceInput input, CancellationToken ct)
    {
        var entry = await catalog.SaveReferenceAsync(Kind(kind), null, input, ct);
        return CreatedAtAction(nameof(Reference), new { kind, id = entry.Id }, entry);
    }
    [HttpPut("{kind:regex(^(categories|suppliers|customers)$)}/{id:guid}"), Authorize(Roles = Editors)]
    public Task<ReferenceDto> UpdateReference(string kind, Guid id, ReferenceInput input, CancellationToken ct) => catalog.SaveReferenceAsync(Kind(kind), id, input, ct);
    private static ReferenceKind Kind(string value) => Enum.Parse<ReferenceKind>(value, true);
}
