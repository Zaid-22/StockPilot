using Microsoft.EntityFrameworkCore;
using StockPilot.Application.Inventory;
using StockPilot.Application.Reports;
using StockPilot.Domain.Catalog;
using StockPilot.Domain.Inventory;
using StockPilot.Domain.Orders;
using StockPilot.Infrastructure.Data;

namespace StockPilot.Infrastructure.Reports;

public sealed class ReportService(ApplicationDbContext db) : IReportService
{
    public async Task<SummaryDto> SummaryAsync(ReportQuery query, CancellationToken ct)
    {
        var start = query.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = query.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var balances = db.Set<InventoryBalance>().AsNoTracking().Where(x => x.WarehouseId == Warehouse.MainId);
        var low = balances.Where(x => x.Product.IsActive && x.QuantityOnHand <= x.Product.ReorderLevel);
        var activeProducts = await db.Set<Product>().CountAsync(x => x.IsActive, ct);
        var units = await balances.SumAsync(x => (long)x.QuantityOnHand, ct);
        var lowCount = await low.CountAsync(ct);
        var lowItems = await low.OrderBy(x => x.QuantityOnHand).ThenBy(x => x.Product.Name).ThenBy(x => x.ProductId).Take(10)
            .Select(x => new BalanceDto(x.ProductId, x.Product.Sku, x.Product.Name, x.Product.IsActive, x.QuantityOnHand, x.Product.ReorderLevel, x.Version)).ToListAsync(ct);
        var open = db.Set<Order>().Where(x => x.Status == OrderStatus.Confirmed || x.Status == OrderStatus.Partial);
        var purchases = await open.CountAsync(x => x.Kind == OrderKind.Purchase, ct);
        var sales = await open.CountAsync(x => x.Kind == OrderKind.Sale, ct);
        var movements = db.Set<StockMovement>().AsNoTracking().Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc < end);
        var daily = await movements.GroupBy(x => x.CreatedAtUtc.Date).Select(g => new
        {
            Date = g.Key,
            Received = g.Sum(x => x.Kind == "Receipt" ? (long)x.QuantityChange : 0),
            Shipped = g.Sum(x => x.Kind == "Shipment" ? -(long)x.QuantityChange : 0),
            Adjusted = g.Sum(x => x.Kind == "Adjustment" ? (long)x.QuantityChange : 0)
        }).ToListAsync(ct);
        var values = from movement in movements
                     join line in db.Set<OrderLine>() on new { OrderId = movement.OrderId, movement.ProductId }
                         equals new { OrderId = (Guid?)line.OrderId, line.ProductId }
                     select new { movement.Kind, movement.QuantityChange, line.UnitPrice };
        var receivedValue = await values.Where(x => x.Kind == "Receipt").SumAsync(x => x.QuantityChange * x.UnitPrice, ct);
        var shippedValue = await values.Where(x => x.Kind == "Shipment").SumAsync(x => -x.QuantityChange * x.UnitPrice, ct);
        var byDate = daily.ToDictionary(x => DateOnly.FromDateTime(x.Date));
        var series = Enumerable.Range(0, query.To.DayNumber - query.From.DayNumber + 1).Select(offset =>
        {
            var date = query.From.AddDays(offset);
            return byDate.TryGetValue(date, out var day) ? new DailyStockDto(date, day.Received, day.Shipped, day.Adjusted) : new DailyStockDto(date, 0, 0, 0);
        }).ToList();
        return new(query.From, query.To, DateTime.UtcNow, activeProducts, units, lowCount, purchases, sales,
            daily.Sum(x => x.Received), daily.Sum(x => x.Shipped), receivedValue, shippedValue, lowItems, series);
    }
}
