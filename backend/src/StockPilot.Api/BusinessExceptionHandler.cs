using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Application.Catalog;
using StockPilot.Application.Common;

namespace StockPilot.Api;

public sealed class BusinessExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BusinessException error) return false;
        context.Response.StatusCode = error.StatusCode;
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = error.StatusCode, Title = "Request failed", Detail = error.Message }
        });
        return true;
    }
}
