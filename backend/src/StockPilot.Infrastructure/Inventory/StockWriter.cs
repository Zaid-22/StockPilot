using Microsoft.EntityFrameworkCore;
using StockPilot.Application.Common;
using StockPilot.Domain.Catalog;
using StockPilot.Domain.Inventory;
using StockPilot.Infrastructure.Data;

namespace StockPilot.Infrastructure.Inventory;

// Caller owns the transaction; balance, journal and command receipt commit together.
public sealed class StockWriter(ApplicationDbContext db)
{
    public async Task<InventoryBalance> ApplyAsync(Guid productId, int delta, Guid operationId,
        string actorId, string kind, string reason, Guid? orderId, Guid? expectedVersion, CancellationToken ct)
    {
        var balance = await db.Set<InventoryBalance>().SingleOrDefaultAsync(
            x => x.ProductId == productId && x.WarehouseId == Warehouse.MainId, ct)
            ?? throw new BusinessException(404, "Product stock not found.");
        if (expectedVersion.HasValue && balance.Version != expectedVersion)
            throw new BusinessException(409, "Stock changed. Reload before adjusting it.");
        var next = (long)balance.QuantityOnHand + delta;
        if (delta == 0 || next < 0 || next > 1000000000)
            throw new BusinessException(409, "Insufficient stock or quantity limit exceeded.");
        balance.QuantityOnHand = (int)next;
        balance.Version = Guid.NewGuid();
        db.Add(new StockMovement
        {
            ProductId = productId, WarehouseId = Warehouse.MainId, OperationId = operationId,
            ActorId = actorId, Kind = kind, Reason = reason, OrderId = orderId,
            QuantityChange = delta, QuantityAfter = balance.QuantityOnHand
        });
        return balance;
    }
}
