namespace StockPilot.Domain.Inventory;

// Append-only journal. Corrections are new movements, never edits to history.
public sealed class StockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid OperationId { get; set; }
    public Guid? OrderId { get; set; }
    public string Kind { get; set; } = "Adjustment";
    public int QuantityChange { get; set; }
    public int QuantityAfter { get; set; }
    public string Reason { get; set; } = "";
    public string ActorId { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

// Stores the original result so a retry cannot repeat a business operation.
public sealed class StockOperation
{
    public Guid Id { get; set; }
    public string Fingerprint { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string ResultJson { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
