namespace StockPilot.Domain.Orders;

public enum OrderKind { Purchase, Sale }
public enum OrderStatus { Draft, Confirmed, Partial, Completed, Cancelled }

public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = "";
    public OrderKind Kind { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? CustomerId { get; set; }
    public string PartyName { get; set; } = "";
    public OrderStatus Status { get; set; }
    public string Notes { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "";
    public Guid Version { get; set; } = Guid.NewGuid();
    public List<OrderLine> Lines { get; set; } = [];
}

public sealed class OrderLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public int ProcessedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
}
