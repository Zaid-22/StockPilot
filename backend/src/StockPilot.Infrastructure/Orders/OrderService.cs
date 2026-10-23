using Microsoft.EntityFrameworkCore;
using StockPilot.Application.Catalog;
using StockPilot.Application.Common;
using StockPilot.Application.Orders;
using StockPilot.Domain.Catalog;
using StockPilot.Domain.Orders;
using StockPilot.Infrastructure.Data;
using StockPilot.Infrastructure.Inventory;

namespace StockPilot.Infrastructure.Orders;

public sealed class OrderService(ApplicationDbContext db, OperationExecutor operations, StockWriter stock) : IOrderService
{
    public async Task<PageResult<OrderSummaryDto>> ListAsync(OrderKind kind, OrderQuery query, CancellationToken ct)
    {
        var orders = db.Set<Order>().AsNoTracking().Where(x => x.Kind == kind);
        if (query.Status.HasValue) orders = orders.Where(x => x.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim().ToUpperInvariant();
            orders = orders.Where(x => x.Number.Contains(term) || x.PartyName.ToUpper().Contains(term));
        }
        var count = await orders.CountAsync(ct);
        var page = await orders.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new(page.Select(x => new OrderSummaryDto(x.Id, x.Number, x.PartyName, x.Status.ToString(), x.CreatedAtUtc)).ToList(), count, query.Page, query.PageSize);
    }
    public async Task<OrderDto> GetAsync(OrderKind kind, Guid id, CancellationToken ct) => ToDto(
        await db.Set<Order>().AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id && x.Kind == kind, ct)
        ?? throw new BusinessException(404, "Order not found."));

    public Task<OrderDto> CreateAsync(OrderKind kind, CreateOrderInput input, string actorId, CancellationToken ct) =>
        operations.ExecuteAsync(input.RequestId, actorId, $"create-{kind}", input, async () =>
        {
            if (kind != OrderKind.Purchase) throw new BusinessException(400, "Unsupported order kind.");
            if (input.Lines is null || input.Lines.Count is < 1 or > 50 || input.Lines.Any(x => x is null)
                || input.Lines.Select(x => x.ProductId).Distinct().Count() != input.Lines.Count)
                throw new BusinessException(400, "Use 1–50 distinct product lines.");
            var supplier = await db.Set<Supplier>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.PartyId && x.IsActive, ct)
                ?? throw new BusinessException(400, "Choose an active supplier.");
            var order = new Order { Kind = kind, SupplierId = supplier.Id, PartyName = supplier.Name,
                Notes = input.Notes?.Trim() ?? "", CreatedBy = actorId };
            order.Number = "PO-" + order.Id.ToString("N").ToUpperInvariant();
            var ids = input.Lines.Select(x => x.ProductId).ToArray();
            var products = await db.Set<Product>().AsNoTracking().Where(x => ids.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, ct);
            foreach (var line in input.Lines)
            {
                if (!products.TryGetValue(line.ProductId, out var product)) throw new BusinessException(400, "Every line must use an active product.");
                order.Lines.Add(new OrderLine { OrderId = order.Id, ProductId = product.Id, Sku = product.Sku,
                    ProductName = product.Name, Quantity = line.Quantity, UnitPrice = line.UnitPrice });
            }
            db.Add(order);
            return ToDto(order);
        }, ct);

    public Task<OrderDto> TransitionAsync(OrderKind kind, Guid id, string action, OrderActionInput input, string actorId, CancellationToken ct) =>
        operations.ExecuteAsync(input.RequestId, actorId, $"{kind}-{id}-{action}", input, async () =>
        {
            var order = await LoadAsync(kind, id, input.Version, ct);
            if (action == "confirm" && order.Status == OrderStatus.Draft)
            {
                // A party or product may have been deactivated after the draft was created.
                if (!await db.Set<Supplier>().AnyAsync(x => x.Id == order.SupplierId && x.IsActive, ct))
                    throw new BusinessException(409, "Supplier is inactive. Cancel this draft and choose an active supplier.");
                var ids = order.Lines.Select(x => x.ProductId).ToArray();
                if (await db.Set<Product>().CountAsync(x => ids.Contains(x.Id) && x.IsActive, ct) != ids.Length)
                    throw new BusinessException(409, "An order product is inactive. Cancel this draft and recreate it.");
                order.Status = OrderStatus.Confirmed;
            }
            else if (action == "cancel" && order.Status is OrderStatus.Draft or OrderStatus.Confirmed && order.Lines.All(x => x.ProcessedQuantity == 0))
                order.Status = OrderStatus.Cancelled;
            else throw new BusinessException(409, "This action is not allowed in the current order state.");
            order.Version = Guid.NewGuid();
            return ToDto(order);
        }, ct);

    public Task<OrderDto> FulfillAsync(OrderKind kind, Guid id, FulfillmentInput input, string actorId, CancellationToken ct) =>
        operations.ExecuteAsync(input.RequestId, actorId, $"fulfill-{kind}-{id}", input, async () =>
        {
            var order = await LoadAsync(kind, id, input.Version, ct);
            if (order.Status is not (OrderStatus.Confirmed or OrderStatus.Partial))
                throw new BusinessException(409, "Only confirmed or partially fulfilled orders can be received.");
            if (string.IsNullOrWhiteSpace(input.Reference) || input.Lines is null || input.Lines.Count is < 1 or > 50
                || input.Lines.Any(x => x is null) || input.Lines.Select(x => x.LineId).Distinct().Count() != input.Lines.Count)
                throw new BusinessException(400, "Provide a delivery reference and distinct order lines.");
            // Consistent product order reduces database deadlock risk across multi-line receipts.
            foreach (var item in input.Lines.OrderBy(x => order.Lines.SingleOrDefault(l => l.Id == x.LineId)?.ProductId))
            {
                var line = order.Lines.SingleOrDefault(x => x.Id == item.LineId)
                    ?? throw new BusinessException(400, "A line does not belong to this order.");
                if (item.Quantity <= 0 || item.Quantity > line.Quantity - line.ProcessedQuantity)
                    throw new BusinessException(409, "Quantity exceeds the outstanding order quantity.");
                await stock.ApplyAsync(line.ProductId, item.Quantity, input.RequestId, actorId, "Receipt",
                    input.Reference.Trim(), order.Id, null, ct);
                line.ProcessedQuantity += item.Quantity;
            }
            order.Status = order.Lines.All(x => x.ProcessedQuantity == x.Quantity) ? OrderStatus.Completed : OrderStatus.Partial;
            order.Version = Guid.NewGuid();
            return ToDto(order);
        }, ct);

    private async Task<Order> LoadAsync(OrderKind kind, Guid id, Guid version, CancellationToken ct)
    {
        var order = await db.Set<Order>().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id && x.Kind == kind, ct)
            ?? throw new BusinessException(404, "Order not found.");
        if (version == Guid.Empty) throw new BusinessException(400, "The current order version is required.");
        if (order.Version != version) throw new BusinessException(409, "Order changed. Reload before trying again.");
        return order;
    }

    private static OrderDto ToDto(Order order) => new(order.Id, order.Number, order.Kind.ToString(), order.PartyName,
        order.Status.ToString(), order.Notes, order.CreatedAtUtc, order.Version,
        order.Lines.Sum(x => x.UnitPrice * x.Quantity), order.Lines.OrderBy(x => x.Sku).Select(x =>
            new OrderLineDto(x.Id, x.ProductId, x.Sku, x.ProductName, x.Quantity, x.ProcessedQuantity, x.UnitPrice, x.Quantity * x.UnitPrice)).ToList());
}
