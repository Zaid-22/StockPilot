using StockPilot.Api;
using StockPilot.Infrastructure.Data;

var app = ApplicationBootstrap.Build(new WebApplicationOptions { Args = args });
if (args.Contains("--initialize"))
{
    await DatabaseInitializer.InitializeAsync(app.Services,
        app.Configuration["Bootstrap:AdminEmail"] ?? "",
        app.Configuration["Bootstrap:AdminPassword"] ?? "");
    return;
}
app.Run();

public partial class Program;
