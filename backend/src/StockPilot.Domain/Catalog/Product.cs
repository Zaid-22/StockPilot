namespace StockPilot.Domain.Catalog;

public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sku { get; set; } = "";
    public string NormalizedSku { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public decimal SellingPrice { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid Version { get; set; } = Guid.NewGuid();
    public ICollection<InventoryBalance> Balances { get; set; } = [];
}

public sealed class Warehouse
{
    public static readonly Guid MainId = Guid.Parse("791f8213-c2e2-46ca-9713-6a61753a1bd2");
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class InventoryBalance
{
    public Guid Version { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public int QuantityOnHand { get; set; }
}
