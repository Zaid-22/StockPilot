using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StockPilot.Application.Catalog;
using StockPilot.Application.Common;
using StockPilot.Domain.Catalog;
using StockPilot.Infrastructure.Data;
using StockPilot.Infrastructure.Inventory;

namespace StockPilot.Infrastructure.Catalog;

public sealed partial class CatalogService(ApplicationDbContext db, OperationExecutor operations) : ICatalogService
{
    private static readonly Expression<Func<Product, ProductDto>> ProductProjection = p => new(
        p.Id, p.Sku, p.Name, p.Description, p.CategoryId, p.Category.Name, p.SellingPrice,
        p.ReorderLevel, p.IsActive, p.Version, p.Balances.Sum(b => b.QuantityOnHand));

    public async Task<PageResult<ProductDto>> ProductsAsync(CatalogQuery query, CancellationToken ct)
    {
        var products = db.Set<Product>().AsNoTracking();
        if (query.IsActive.HasValue) products = products.Where(p => p.IsActive == query.IsActive.Value);
        if (query.CategoryId.HasValue) products = products.Where(p => p.CategoryId == query.CategoryId.Value);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim().ToUpperInvariant();
            products = products.Where(p => p.Name.ToUpper().Contains(term) || p.NormalizedSku.Contains(term));
        }
        var count = await products.CountAsync(ct);
        var items = await products.OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(ProductProjection).ToListAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }

    public async Task<ProductDto> ProductAsync(Guid id, CancellationToken ct) =>
        await db.Set<Product>().AsNoTracking().Where(p => p.Id == id).Select(ProductProjection).SingleOrDefaultAsync(ct)
        ?? throw new BusinessException(404, "Product not found.");

    public Task<ProductDto> SaveProductAsync(Guid? id, ProductInput input, string actorId, CancellationToken ct) =>
        id.HasValue ? SaveProductCoreAsync(id, input, ct)
            : operations.ExecuteAsync(input.RequestId, actorId, "create-catalog-product", input,
                () => SaveProductCoreAsync(null, input, ct), ct);

    private async Task<ProductDto> SaveProductCoreAsync(Guid? id, ProductInput input, CancellationToken ct)
    {
        var sku = input.Sku.Trim().ToUpperInvariant();
        if (!SkuPattern().IsMatch(sku)) throw new BusinessException(400, "SKU must use 1–64 letters, numbers, dots, underscores or hyphens, starting with a letter or number.");
        if (string.IsNullOrWhiteSpace(input.Name)) throw new BusinessException(400, "Product name is required.");
        var product = id.HasValue
            ? await db.Set<Product>().SingleOrDefaultAsync(p => p.Id == id.Value, ct) ?? throw new BusinessException(404, "Product not found.")
            : new Product();
        if (id.HasValue) RequireVersion(input.Version, product.Version);
        var category = await db.Set<Category>().SingleOrDefaultAsync(c => c.Id == input.CategoryId, ct);
        if (category is null || (!category.IsActive && product.CategoryId != category.Id))
            throw new BusinessException(400, "Choose an active category.");
        if (await db.Set<Product>().AnyAsync(p => p.NormalizedSku == sku && p.Id != product.Id, ct))
            throw new BusinessException(409, "This SKU is already used, including by an inactive product.");
        product.Sku = sku;
        product.NormalizedSku = sku;
        product.Name = input.Name.Trim();
        product.Description = Clean(input.Description);
        product.CategoryId = input.CategoryId;
        product.SellingPrice = input.SellingPrice;
        product.ReorderLevel = input.ReorderLevel;
        product.IsActive = input.IsActive;
        product.Version = Guid.NewGuid();
        if (!id.HasValue)
        {
            product.Balances.Add(new InventoryBalance { ProductId = product.Id, WarehouseId = Warehouse.MainId });
            db.Add(product);
            // OperationExecutor commits the product, zero balance, and retry result together.
            return new(product.Id, product.Sku, product.Name, product.Description, category.Id, category.Name,
                product.SellingPrice, product.ReorderLevel, product.IsActive, product.Version, 0);
        }
        await SaveAsync(ct);
        return await ProductAsync(product.Id, ct);
    }

    public Task<PageResult<ReferenceDto>> ReferencesAsync(ReferenceKind kind, CatalogQuery query, CancellationToken ct) => kind switch
    {
        ReferenceKind.Categories => ListAsync<Category>(query, ct),
        ReferenceKind.Suppliers => ListAsync<Supplier>(query, ct),
        ReferenceKind.Customers => ListAsync<Customer>(query, ct),
        _ => throw new BusinessException(404, "Catalog not found.")
    };
    public Task<ReferenceDto> ReferenceAsync(ReferenceKind kind, Guid id, CancellationToken ct) => kind switch
    {
        ReferenceKind.Categories => GetAsync<Category>(id, ct),
        ReferenceKind.Suppliers => GetAsync<Supplier>(id, ct),
        ReferenceKind.Customers => GetAsync<Customer>(id, ct),
        _ => throw new BusinessException(404, "Catalog not found.")
    };
    public Task<ReferenceDto> SaveReferenceAsync(ReferenceKind kind, Guid? id, ReferenceInput input, string actorId, CancellationToken ct) =>
        id.HasValue ? SaveReferenceCoreAsync(kind, id, input, ct)
            : operations.ExecuteAsync(input.RequestId, actorId, $"create-catalog-{kind}", input,
                () => SaveReferenceCoreAsync(kind, null, input, ct), ct);

    private Task<ReferenceDto> SaveReferenceCoreAsync(ReferenceKind kind, Guid? id, ReferenceInput input, CancellationToken ct) => kind switch
    {
        ReferenceKind.Categories => SaveRecordAsync<Category>(id, input, ct),
        ReferenceKind.Suppliers => SaveRecordAsync<Supplier>(id, input, ct),
        ReferenceKind.Customers => SaveRecordAsync<Customer>(id, input, ct),
        _ => throw new BusinessException(404, "Catalog not found.")
    };

    private async Task<PageResult<ReferenceDto>> ListAsync<T>(CatalogQuery query, CancellationToken ct) where T : class, INamedRecord
    {
        var records = db.Set<T>().AsNoTracking();
        if (query.IsActive.HasValue) records = records.Where(r => r.IsActive == query.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim().ToUpperInvariant();
            records = records.Where(r => r.NormalizedName.Contains(term));
        }
        var count = await records.CountAsync(ct);
        var items = await records.OrderBy(r => r.Name).ThenBy(r => r.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new(items.Select(ToDto).ToArray(), count, query.Page, query.PageSize);
    }
    private async Task<ReferenceDto> GetAsync<T>(Guid id, CancellationToken ct) where T : class, INamedRecord =>
        ToDto(await db.Set<T>().AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new BusinessException(404, "Record not found."));

    private async Task<ReferenceDto> SaveRecordAsync<T>(Guid? id, ReferenceInput input, CancellationToken ct) where T : class, INamedRecord, new()
    {
        var name = input.Name.Trim();
        if (name.Length == 0) throw new BusinessException(400, "Name is required.");
        var record = id.HasValue
            ? await db.Set<T>().SingleOrDefaultAsync(r => r.Id == id.Value, ct) ?? throw new BusinessException(404, "Record not found.")
            : new T();
        if (id.HasValue) RequireVersion(input.Version, record.Version);
        var normalized = name.ToUpperInvariant();
        if (typeof(T) == typeof(Category) && await db.Set<Category>().AnyAsync(c => c.NormalizedName == normalized && c.Id != record.Id, ct))
            throw new BusinessException(409, "A category with this name already exists.");
        record.Name = name;
        record.NormalizedName = normalized;
        record.IsActive = input.IsActive;
        record.Version = Guid.NewGuid();
        if (record is IContactRecord contact)
        {
            contact.Email = Clean(input.Email);
            contact.Phone = Clean(input.Phone);
            contact.Address = Clean(input.Address);
        }
        if (!id.HasValue) db.Add(record);
        else await SaveAsync(ct);
        return ToDto(record);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new BusinessException(409, "This record changed. Reload it before saving again."); }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new BusinessException(409, "A record with this SKU or category name already exists."); }
    }
    private static ReferenceDto ToDto(INamedRecord r) => new(r.Id, r.Name, r.IsActive, r.Version,
        (r as IContactRecord)?.Email, (r as IContactRecord)?.Phone, (r as IContactRecord)?.Address);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void RequireVersion(Guid? expected, Guid actual)
    {
        if (!expected.HasValue || expected.Value == Guid.Empty) throw new BusinessException(400, "A version is required when editing a record.");
        if (expected != actual) throw new BusinessException(409, "This record changed. Reload it before saving again.");
    }
    [GeneratedRegex("^[A-Z0-9][A-Z0-9._-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SkuPattern();
}
