using System.ComponentModel.DataAnnotations;
using StockPilot.Application.Catalog;

namespace StockPilot.Application.Inventory;

public sealed class StockQuery
{
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [MaxLength(160)] public string? Q { get; set; }
    public bool LowStockOnly { get; set; }
    public Guid? ProductId { get; set; }
}

public sealed class AdjustmentInput : IValidatableObject
{
    public Guid RequestId { get; set; }
    public Guid ProductId { get; set; }
    public Guid Version { get; set; }
    [Range(-1000000000, 1000000000)] public int QuantityChange { get; set; }
    [Required, MaxLength(500)] public string Reason { get; set; } = "";
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (RequestId == Guid.Empty || ProductId == Guid.Empty || Version == Guid.Empty)
            yield return new("Request ID, product and current stock version are required.");
        if (QuantityChange == 0) yield return new("Quantity change cannot be zero.", [nameof(QuantityChange)]);
    }
}

public sealed record BalanceDto(Guid ProductId, string Sku, string Name, bool IsActive,
    int QuantityOnHand, int ReorderLevel, Guid Version);
public sealed record MovementDto(Guid Id, Guid ProductId, string Sku, string ProductName,
    string Kind, int QuantityChange, int QuantityAfter, string Reason, string ActorId,
    DateTime CreatedAtUtc, Guid OperationId, Guid? OrderId);
public sealed record AdjustmentResult(Guid OperationId, int QuantityOnHand, Guid Version);

public interface IInventoryService
{
    Task<PageResult<BalanceDto>> BalancesAsync(StockQuery query, CancellationToken ct);
    Task<PageResult<MovementDto>> MovementsAsync(StockQuery query, CancellationToken ct);
    Task<AdjustmentResult> AdjustAsync(AdjustmentInput input, string actorId, CancellationToken ct);
}
