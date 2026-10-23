namespace StockPilot.Application.Common;

public sealed class BusinessException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
