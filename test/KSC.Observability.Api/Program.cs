using KSC.Observability;
using KSC.Observability.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ServiceName باید ClientId اپ در پنل KSC.Authorization باشد
builder.AddKscObservability(o =>
{
    o.ServiceName = "sample-app";
    o.OtlpEndpoint = "http://localhost:4317";   // OTel Collector
    o.Dependencies.Add(new KscDependencyOptions { Name = "Redis", Type = KscDependencyType.Tcp, Target = "127.0.0.1:6379", Critical = false });
    o.Dependencies.Add(new KscDependencyOptions { Name = "Purchase DB", Type = KscDependencyType.Sql, Target = builder.Configuration.GetConnectionString("Purchase") ?? "Server=127.0.0.1,1433;Database=Purchase;User ID=sa;Password=x;TrustServerCertificate=True;Connect Timeout=3" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseKscObservability();   // X-Trace-Id + شمارش exceptionهای مدیریت‌نشده
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapKscObservability();   // /ksc/observability/info و /health

app.Run();
