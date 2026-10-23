using Microsoft.AspNetCore.Mvc;
using StockPilot.Application.Reports;

namespace StockPilot.Api.Controllers;

[ApiController, Route("api/reports")]
public sealed class ReportsController(IReportService reports) : ControllerBase
{
    [HttpGet("summary")]
    public Task<SummaryDto> Summary([FromQuery] ReportQuery query, CancellationToken ct) => reports.SummaryAsync(query, ct);
}
