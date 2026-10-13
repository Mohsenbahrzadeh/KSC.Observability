# KSC.Observability

کتابخانه‌ی Observability سازمان برای اپلیکیشن‌های .NET — متریک، تریس، لاگ، SQL و سلامت وابستگی‌ها از طریق **OpenTelemetry (OTLP)**.
داده‌ی همین کتابخانه، داشبورد «Observability» پنل **KSC.Authorization** را تغذیه می‌کند (برنامه‌نویس اپ را انتخاب می‌کند و داشبورد اپ خودش را می‌بیند).

| پکیج | هدف |
|---|---|
| `KSC.Observability` | .NET 8 (ASP.NET Core، Blazor، Web API) |
| `KSC.Observability.AspNet` | .NET Framework 4.8 (MVC 5، Web API 2، Web Forms) |

```
اپ ──OTLP──▶ OTel Collector ─┬─ متریک ▶ Prometheus ─┐
                             ├─ لاگ    ▶ Loki       ├─▶ داشبورد پنل KSC.Authorization
                             └─ تریس   ▶ Tempo      ┘
```

## ۱) راه‌اندازی در .NET 8

```csharp
// Program.cs
using KSC.Observability;
using KSC.Observability.Extensions;

builder.AddKscObservability(o =>
{
    o.ServiceName = "my-client-id";                 // ⚠ دقیقاً ClientId اپ در پنل KSC.Authorization
    o.OtlpEndpoint = "http://otel-collector:4317";

    // وابستگی‌هایی که باید پایش سلامت شوند
    o.Dependencies.Add(new() { Name = "Purchase DB", Type = KscDependencyType.Sql,  Target = builder.Configuration.GetConnectionString("Purchase")! });
    o.Dependencies.Add(new() { Name = "Redis",       Type = KscDependencyType.Tcp,  Target = "redis:6379", Critical = false });
    o.Dependencies.Add(new() { Name = "HR API",      Type = KscDependencyType.Http, Target = "https://hr.ksc.ir/health/live" });
});

var app = builder.Build();
app.UseKscObservability();   // X-Trace-Id روی پاسخ‌ها + شمارش exceptionهای مدیریت‌نشده (بالای سایر middlewareها)
...
app.MapKscObservability();   // /ksc/observability/info و /ksc/observability/health
```

همه‌ی گزینه‌ها از `appsettings.json` (بخش `KscObservability`) هم خوانده می‌شود:

```json
"KscObservability": {
  "ServiceName": "my-client-id",
  "OtlpEndpoint": "http://otel-collector:4317",
  "TraceSampleRatio": 0.2,
  "RecordSqlStatements": false,
  "Dependencies": [
    { "Name": "Purchase DB", "Type": "Sql", "Target": "Server=...;", "IntervalSeconds": 30 }
  ]
}
```

## ۲) راه‌اندازی در .NET Framework 4.8

```xml
<!-- web.config → appSettings (راه‌اندازی خودکار) -->
<add key="Ksc:Observability:ServiceName" value="my-client-id" />
<add key="Ksc:Observability:OtlpEndpoint" value="http://otel-collector:4317" />
```
یا در `Global.asax.cs`:
```csharp
protected void Application_Start()
{
    KscObservability.Start(o =>
    {
        o.ServiceName = "my-client-id";
        o.Dependencies.Add(new KscDependencyOptions { Name = "Main DB", Type = KscDependencyType.Sql, Target = ConfigurationManager.ConnectionStrings["Main"].ConnectionString });
    });
}
```
ماژول‌های `TelemetryHttpModule` و `KscObservabilityModule` در حالت خودکار ثبت می‌شوند؛ در حالت دستی در `system.webServer/modules` اضافه کنید.
(در net48 لاگ OTLP پشتیبانی نمی‌شود؛ متریک و تریس و SQL و سلامت وابستگی‌ها کامل است.)

## ۳) چه چیزی خودکار جمع می‌شود؟

| حوزه | متریک / تریس |
|---|---|
| HTTP ورودی | `http.server.request.duration` (rate، خطا، p50/p95/p99، route) + تریس |
| HTTP خروجی | `http.client.request.duration` (به تفکیک سرور مقصد) + تریس |
| SQL | `db.client.operation.duration` + span هر کوئری (متن کوئری فقط با `RecordSqlStatements=true`، بدون مقدار پارامترها) |
| Runtime | GC، توقف GC، Thread Pool، Exception، حافظه، CPU |
| uptime / restart | `ksc.app.start_time` |
| خطای مدیریت‌نشده | `ksc.http.unhandled_exceptions` (نوع + route) و هدر `X-Trace-Id` |
| سلامت وابستگی‌ها | `ksc.dependency.up`، `ksc.dependency.check.duration`، `ksc.dependency.check.failures` |
| لاگ | همه‌ی `ILogger` با TraceId/SpanId (فقط .NET 8) |
| کسب‌وکاری | `KscTelemetry.Counter/Histogram/Gauge` (مثلاً `orders.created`) |

متریک کسب‌وکاری:
```csharp
static readonly Counter<long> Orders = KscTelemetry.Counter("orders.created", "{order}");
Orders.Add(1, new KeyValuePair<string, object?>("channel", "web"));
```

## ۴) Endpointها (بدون احراز هویت، فقط اطلاعات عملیاتی)

- `GET /ksc/observability/info` — نام، نسخه، محیط، uptime، وضعیت وابستگی‌ها.
- `GET /ksc/observability/health` — `200` یا `503` (اگر وابستگی `Critical` خراب باشد)؛ مناسب Load-Balancer.

## ۵) زیرساخت

- `deploy/docker-compose.yml` — استک کامل برای توسعه/تست (`docker compose -f deploy/docker-compose.yml up -d`: Collector + Prometheus + Loki + Tempo).
- `deploy/otelcol.example.yaml` — Collector (متریک→Prometheus، لاگ→Loki، تریس→Tempo). گزینه‌ی `resource_to_telemetry_conversion` الزامی است تا `service_instance_id` و `service_version` لیبل شوند.
- `deploy/alerts.rules.yml` — قوانین هشدار (خطا، تأخیر، قطع داده، restart، وابستگی خراب، SQL کند). پنل هشدارهای فعال را در بنر سلامت نشان می‌دهد.

## ۶) نکات امنیتی

- مقدار پارامترهای SQL هرگز ثبت نمی‌شود؛ متن کوئری به‌صورت پیش‌فرض خاموش است.
- پیام خطای probe وابستگی‌ها فقط نوع خطا را نگه می‌دارد (connection string یا آدرس داخلی نشت نمی‌کند).
- مسیرهای `/health` و `/ksc/observability` از تریس حذف می‌شوند.
- نرخ نمونه‌برداری تریس را در ترافیک سنگین پایین بیاورید (`TraceSampleRatio`).

## ۷) نام متریک‌ها در Prometheus (تأییدشده با استک واقعی)

لیبل `job` = `service.name` (ClientId). نام‌ها: `http_server_request_duration_seconds_*` (لیبل‌های `http_route`, `http_response_status_code`)،
`http_client_request_duration_seconds_*` (`server_address`)، `db_client_operation_duration_seconds_*` (`db_namespace`, `db_system_name=microsoft.sql_server`)،
`process_runtime_dotnet_*` (Runtime روی .NET 8؛ روی .NET 9+ نام‌ها `dotnet_*` است و پنل هر دو را می‌خواند)، `process_memory_usage_bytes`، `process_cpu_time_seconds_total`،
`ksc_app_start_time_seconds`, `ksc_dependency_up`, `ksc_dependency_check_duration_seconds_*`, `ksc_http_unhandled_exceptions_total`.

نکات:
- در `prometheus.yml` برای scrape کردن Collector حتماً `honor_labels: true` بگذارید (`deploy/prometheus.stack.yml`).
- متریک HTTP-clientِ خودِ exporter با فیلتر `filter/drop_otlp_self` در Collector حذف می‌شود تا Collector به‌عنوان «وابستگی» اپ دیده نشود.
- پس از تغییر نسخه‌ی کتابخانه، سری‌های قدیمی (با مرز باکت متفاوت) تا چند دقیقه در `rate()` کنار سری جدید می‌مانند.

## ۸) عیب‌یابی

| علامت | علت محتمل |
|---|---|
| داشبورد «بدون داده» | `ServiceName` با ClientId یکی نیست، یا Collector در دسترس نیست |
| نمودار SQL خالی | `EnableSqlInstrumentation=false` یا اپ از ADO.NET دیگری (غیر SqlClient) استفاده می‌کند |
| نام متریک‌ها فرق دارد | کانفیگ exporter در Collector؛ کوئری‌های پنل در `ObservabilityService.cs` (`Specs`) قابل اصلاح‌اند |
