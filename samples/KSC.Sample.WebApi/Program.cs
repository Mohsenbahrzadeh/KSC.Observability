using KSC.Observability;
using KSC.Observability.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1) Register the observability runtime. Options can also come from the
//    "KSC.Observability" section of appsettings.json (see appsettings.json).
builder.Services.AddKscObservability(options =>
{
    options.ServiceName = "sample-web-api";
    options.Environment = "development";
});

var app = builder.Build();

// 2) One line wires request metrics + active-user tracking + the /metrics endpoint.
app.UseKscObservability();

app.MapGet("/", () => "KSC.Observability ASP.NET Core sample — metrics at /metrics");

app.MapGet("/work", async () =>
{
    // A little artificial work so the latency histogram has something to show.
    await Task.Delay(Random.Shared.Next(5, 120));
    return Results.Ok(new { status = "ok", at = DateTime.UtcNow });
});

app.Run();
