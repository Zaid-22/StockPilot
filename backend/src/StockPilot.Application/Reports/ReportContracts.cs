using System.ComponentModel.DataAnnotations;
using StockPilot.Application.Inventory;

namespace StockPilot.Application.Reports;

public sealed class ReportQuery : IValidatableObject
{
    public DateOnly From { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-29);
    public DateOnly To { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (To < From || To.DayNumber - From.DayNumber > 365 || To == DateOnly.MaxValue)
            yield return new("Choose a date range of 1–366 days, from oldest to newest.");
    }
}
public sealed record DailyStockDto(DateOnly Date, long Received, long Shipped, long Adjusted);
public sealed record SummaryDto(DateOnly From, DateOnly To, DateTime GeneratedAtUtc,
    int ActiveProducts, long UnitsOnHand, int LowStockProducts, int OpenPurchaseOrders, int OpenSalesOrders,
    long UnitsReceived, long UnitsShipped, decimal ReceivedOrderValue, decimal ShippedOrderValue,
    IReadOnlyList<BalanceDto> LowStock, IReadOnlyList<DailyStockDto> DailyStock);

public interface IReportService
{
    Task<SummaryDto> SummaryAsync(ReportQuery query, CancellationToken ct);
}
