namespace StockPilot.Domain.Catalog;

public interface INamedRecord
{
    Guid Id { get; set; }
    string Name { get; set; }
    string NormalizedName { get; set; }
    bool IsActive { get; set; }
    Guid Version { get; set; }
}

public interface IContactRecord : INamedRecord
{
    string? Email { get; set; }
    string? Phone { get; set; }
    string? Address { get; set; }
}

public enum ReferenceKind { Categories, Suppliers, Customers }

public sealed class Category : INamedRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public Guid Version { get; set; } = Guid.NewGuid();
}

public sealed class Supplier : IContactRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public Guid Version { get; set; } = Guid.NewGuid();
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}

public sealed class Customer : IContactRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public Guid Version { get; set; } = Guid.NewGuid();
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}
