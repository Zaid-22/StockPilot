using Microsoft.EntityFrameworkCore;
using StockPilot.Application.Catalog;
using StockPilot.Application.Common;
using StockPilot.Application.Inventory;
using StockPilot.Domain.Catalog;
using StockPilot.Domain.Inventory;
using StockPilot.Infrastructure.Data;

namespace StockPilot.Infrastructure.Inventory;

public sealed class InventoryService(ApplicationDbContext db, OperationExecutor operations, StockWriter writer) : IInventoryService
{
    public async Task<PageResult<BalanceDto>> BalancesAsync(StockQuery query, CancellationToken ct)
    {
        var rows = db.Set<InventoryBalance>().AsNoTracking().Where(x => x.WarehouseId == Warehouse.MainId);
        if (query.ProductId.HasValue) rows = rows.Where(x => x.ProductId == query.ProductId);
        if (query.LowStockOnly) rows = rows.Where(x => x.Product.IsActive && x.QuantityOnHand <= x.Product.ReorderLevel);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim().ToUpperInvariant();
            rows = rows.Where(x => x.Product.NormalizedSku.Contains(term) || x.Product.Name.ToUpper().Contains(term));
        }
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.Product.Name).ThenBy(x => x.ProductId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new BalanceDto(x.ProductId, x.Product.Sku, x.Product.Name, x.Product.IsActive,
                x.QuantityOnHand, x.Product.ReorderLevel, x.Version)).ToListAsync(ct);
        return new(items, total, query.Page, query.PageSize);
    }

    public async Task<PageResult<MovementDto>> MovementsAsync(StockQuery query, CancellationToken ct)
    {
        var rows = from m in db.Set<StockMovement>().AsNoTracking()
                   join p in db.Set<Product>() on m.ProductId equals p.Id
                   select new { m, p };
        if (query.ProductId.HasValue) rows = rows.Where(x => x.m.ProductId == query.ProductId);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim().ToUpperInvariant();
            rows = rows.Where(x => x.p.NormalizedSku.Contains(term) || x.p.Name.ToUpper().Contains(term));
        }
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.m.CreatedAtUtc).ThenByDescending(x => x.m.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new MovementDto(x.m.Id, x.p.Id, x.p.Sku, x.p.Name, x.m.Kind,
                x.m.QuantityChange, x.m.QuantityAfter, x.m.Reason, x.m.ActorId, x.m.CreatedAtUtc,
                x.m.OperationId, x.m.OrderId)).ToListAsync(ct);
        return new(items, total, query.Page, query.PageSize);
    }

    public Task<AdjustmentResult> AdjustAsync(AdjustmentInput input, string actorId, CancellationToken ct) =>
        operations.ExecuteAsync(input.RequestId, actorId, "adjustment", input, async () =>
        {
            if (string.IsNullOrWhiteSpace(input.Reason)) throw new BusinessException(400, "An adjustment reason is required.");
            if (!await db.Set<Product>().AnyAsync(x => x.Id == input.ProductId && x.IsActive, ct))
                throw new BusinessException(400, "Choose an active product.");
            var balance = await writer.ApplyAsync(input.ProductId, input.QuantityChange, input.RequestId,
                actorId, "Adjustment", input.Reason.Trim(), null, input.Version, ct);
            return new AdjustmentResult(input.RequestId, balance.QuantityOnHand, balance.Version);
        }, ct);
}
