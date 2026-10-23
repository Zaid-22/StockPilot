using System.ComponentModel.DataAnnotations;
using StockPilot.Domain.Catalog;

namespace StockPilot.Application.Catalog;

public sealed class CatalogQuery
{
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [MaxLength(160)] public string? Q { get; set; }
    public bool? IsActive { get; set; }
    public Guid? CategoryId { get; set; }
}

public sealed record PageResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
public sealed record ReferenceDto(Guid Id, string Name, bool IsActive, Guid Version,
    string? Email, string? Phone, string? Address);
public sealed record ProductDto(Guid Id, string Sku, string Name, string? Description,
    Guid CategoryId, string CategoryName, decimal SellingPrice, int ReorderLevel,
    bool IsActive, Guid Version, int QuantityOnHand);

public sealed class ReferenceInput
{
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    [EmailAddress, MaxLength(256)] public string? Email { get; set; }
    [MaxLength(40)] public string? Phone { get; set; }
    [MaxLength(500)] public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? Version { get; set; }
}

public sealed class ProductInput : IValidatableObject
{
    [Required, MaxLength(64)] public string Sku { get; set; } = "";
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    [MaxLength(2000)] public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    [Range(typeof(decimal), "0", "999999999.999")] public decimal SellingPrice { get; set; }
    [Range(0, 1000000000)] public int ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? Version { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (decimal.Round(SellingPrice, 3) != SellingPrice)
            yield return new ValidationResult("Use at most three decimal places.", [nameof(SellingPrice)]);
        if (CategoryId == Guid.Empty)
            yield return new ValidationResult("Choose a category.", [nameof(CategoryId)]);
    }
}

public interface ICatalogService
{
    Task<PageResult<ProductDto>> ProductsAsync(CatalogQuery query, CancellationToken ct);
    Task<ProductDto> ProductAsync(Guid id, CancellationToken ct);
    Task<ProductDto> SaveProductAsync(Guid? id, ProductInput input, CancellationToken ct);
    Task<PageResult<ReferenceDto>> ReferencesAsync(ReferenceKind kind, CatalogQuery query, CancellationToken ct);
    Task<ReferenceDto> ReferenceAsync(ReferenceKind kind, Guid id, CancellationToken ct);
    Task<ReferenceDto> SaveReferenceAsync(ReferenceKind kind, Guid? id, ReferenceInput input, CancellationToken ct);
}
