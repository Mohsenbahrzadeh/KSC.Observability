# KSC.Observability

پروژه محلی برای راه‌اندازی **Observability Stack** مبتنی بر OpenTelemetry و Prometheus، احتمالاً برای مانیتورینگ **KSC.ERP (KSC)**.

این پروژه شامل ابزارهای لازم برای جمع‌آوری، پردازش و نمایش متریک‌های سیستم (به ویژه متریک‌های صادرشده توسط KSC روی پورت Prometheus) است و امکان مانیتورینگ پیشرفته را فراهم می‌کند.

## ویژگی‌ها
- اجرای **OpenTelemetry Collector** برای جمع‌آوری و پردازش telemetry data (metrics, traces, logs)
- اجرای **Prometheus** برای scraping و ذخیره‌سازی متریک‌ها
- اسکریپت PowerShell ساده برای راه‌اندازی همزمان سرویس‌ها
- مناسب برای محیط‌های محلی/تست یا مانیتورینگ KSC با استفاده از exporter داخلی Prometheus آن (پورت 13296)

## پیش‌نیازها
- Windows (به دلیل فایل‌های .exe و اسکریپت PowerShell)
- فایل‌های باینری دانلودشده:
  - `otelcol.exe` (OpenTelemetry Collector Contrib)
  - `prometheus.exe`
  - `promtool.exe` (اختیاری)




## استفاده در اپلیکیشن‌ها (نسخه‌ی 1.1.0)

```csharp
// Program.cs
builder.AddKscObservability(o => o.ServiceName = "my-client-id");   // = ClientId اپ در پنل KSC.Authorization
...
app.MapKscObservabilityInfo();   // اختیاری: /ksc/observability/info
```

یا از `appsettings.json` (بخش `KscObservability`): `ServiceName`, `OtlpEndpoint`, `Protocol`, `EnableMetrics/Tracing/Logging`, `IgnoredPaths`, `AdditionalMeters`, `AdditionalActivitySources`.

- `ServiceName` **باید برابر ClientId** اپ در پنل باشد؛ داشبورد پنل بر اساس همین نام داده را فیلتر می‌کند.
- سه سیگنال متریک/تریس/لاگ از OTLP ارسال می‌شود. نمونه‌ی کانفیگ collector: `deploy/otelcol.example.yaml`.
- API قدیمی `builder.Host.AddKscObservability("name", "http://localhost:4317")` هم کار می‌کند.
