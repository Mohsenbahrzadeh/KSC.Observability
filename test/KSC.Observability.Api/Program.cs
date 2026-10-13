using KSC.Observability;
using KSC.Observability.Extensions;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var purchaseCs = builder.Configuration.GetConnectionString("Purchase")
    ?? "Server=127.0.0.1,1433;Database=master;User ID=sa;Password=x;TrustServerCertificate=True;Connect Timeout=3";

// ServiceName باید ClientId اپ در پنل KSC.Authorization باشد.
// OtlpEndpoint از appsettings (KscObservability:OtlpEndpoint) یا متغیر محیطی KscObservability__OtlpEndpoint
builder.AddKscObservability(o =>
{
    o.ServiceName = "sample-app";
    o.Dependencies.Add(new KscDependencyOptions { Name = "Redis", Type = KscDependencyType.Tcp, Target = "127.0.0.1:6379", Critical = false });
    o.Dependencies.Add(new KscDependencyOptions { Name = "Purchase DB", Type = KscDependencyType.Sql, Target = purchaseCs });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseKscObservability();   // X-Trace-Id + شمارش exceptionهای مدیریت‌نشده
app.UseAuthorization();
app.MapControllers();
app.MapKscObservability();   // /ksc/observability/info و /health

// ─── endpointهای آزمایشی برای تولید داده ───
app.MapGet("/db", async () =>
{
    await using var c = new SqlConnection(purchaseCs);
    await c.OpenAsync();
    await using var cmd = new SqlCommand("SELECT COUNT(*) FROM sys.objects", c);
    return Results.Ok(await cmd.ExecuteScalarAsync());
});
app.MapGet("/slow", async () => { await Task.Delay(Random.Shared.Next(200, 1400)); return Results.Ok("slow"); });
app.MapGet("/boom", () => { throw new InvalidOperationException("boom"); });
app.MapGet("/bad", () => Results.StatusCode(503));

app.Run();
