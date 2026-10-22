var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
var app = builder.Build();
app.UseExceptionHandler();
app.MapGet("/api/health/live", () => Results.Ok(new { status = "ok", application = "StockPilot" }));
app.Run();
public partial class Program;
