using System.ComponentModel.DataAnnotations;
using StockPilot.Application.Catalog;
using StockPilot.Domain.Orders;

namespace StockPilot.Application.Orders;

public sealed class OrderQuery
{
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [MaxLength(160)] public string? Q { get; set; }
    [EnumDataType(typeof(OrderStatus))] public OrderStatus? Status { get; set; }
}

public sealed class OrderLineInput : IValidatableObject
{
    public Guid ProductId { get; set; }
    [Range(1, 1000000)] public int Quantity { get; set; }
    [Range(typeof(decimal), "0", "999999999.999")] public decimal UnitPrice { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ProductId == Guid.Empty) yield return new("Choose a product.");
        if (decimal.Round(UnitPrice, 3) != UnitPrice) yield return new("Unit price supports at most three decimal places.");
    }
}

public sealed class CreateOrderInput : IValidatableObject
{
    public Guid RequestId { get; set; }
    public Guid PartyId { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    [Required, MinLength(1), MaxLength(50)] public List<OrderLineInput> Lines { get; set; } = [];
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (RequestId == Guid.Empty || PartyId == Guid.Empty) yield return new("Request ID and supplier/customer are required.");
        if (Lines is not null && Lines.Any(x => x is null)) yield return new("Order lines cannot be null.");
    }
}

public class OrderActionInput : IValidatableObject
{
    public Guid RequestId { get; set; }
    public Guid Version { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (RequestId == Guid.Empty || Version == Guid.Empty) yield return new("Request ID and current order version are required.");
    }
}
public sealed class FulfillmentLineInput
{
    public Guid LineId { get; set; }
    [Range(1, 1000000)] public int Quantity { get; set; }
}
public sealed class FulfillmentInput : OrderActionInput
{
    [Required, MinLength(1), MaxLength(50)] public List<FulfillmentLineInput> Lines { get; set; } = [];
    [Required, MaxLength(500)] public string Reference { get; set; } = "";
}

public sealed record OrderLineDto(Guid Id, Guid ProductId, string Sku, string ProductName,
    int Quantity, int ProcessedQuantity, decimal UnitPrice, decimal Total);
public sealed record OrderDto(Guid Id, string Number, string Kind, string PartyName, string Status,
    string Notes, DateTime CreatedAtUtc, Guid Version, decimal Total, IReadOnlyList<OrderLineDto> Lines);
public sealed record OrderSummaryDto(Guid Id, string Number, string PartyName, string Status, DateTime CreatedAtUtc);

public interface IOrderService
{
    Task<PageResult<OrderSummaryDto>> ListAsync(OrderKind kind, OrderQuery query, CancellationToken ct);
    Task<OrderDto> GetAsync(OrderKind kind, Guid id, CancellationToken ct);
    Task<OrderDto> CreateAsync(OrderKind kind, CreateOrderInput input, string actorId, CancellationToken ct);
    Task<OrderDto> TransitionAsync(OrderKind kind, Guid id, string action, OrderActionInput input, string actorId, CancellationToken ct);
    Task<OrderDto> FulfillAsync(OrderKind kind, Guid id, FulfillmentInput input, string actorId, CancellationToken ct);
}
