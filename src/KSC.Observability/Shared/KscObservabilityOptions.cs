using System;
using System.Collections.Generic;
using OpenTelemetry.Exporter;

namespace KSC.Observability
{
    public enum KscDependencyType { Sql, Http, Tcp }

    /// <summary>یک وابستگی بیرونی که به‌صورت دوره‌ای بررسی (probe) می‌شود (SQL، سرویس HTTP، پورت TCP مثل Redis/RabbitMQ).</summary>
    public sealed class KscDependencyOptions
    {
        /// <summary>نام نمایشی (در داشبورد دیده می‌شود)؛ مثلاً «Purchase DB».</summary>
        public string Name { get; set; } = "";
        public KscDependencyType Type { get; set; } = KscDependencyType.Http;
        /// <summary>Sql: connection string — Http: URL — Tcp: host:port</summary>
        public string Target { get; set; } = "";
        /// <summary>اگر true و وابستگی خراب باشد، endpoint سلامت (/ksc/observability/health) خطای 503 می‌دهد.</summary>
        public bool Critical { get; set; } = true;
        public int IntervalSeconds { get; set; } = 30;
        public int TimeoutSeconds { get; set; } = 5;
    }

    /// <summary>
    /// تنظیمات Observability اپلیکیشن. از بخش <c>KscObservability</c> در appsettings هم خوانده می‌شود.
    /// </summary>
    public sealed class KscObservabilityOptions
    {
        public const string SectionName = "KscObservability";

        /// <summary>
        /// نام سرویس (service.name). باید دقیقاً برابر <b>ClientId</b> اپلیکیشن در پنل KSC.Authorization باشد
        /// تا داشبورد Observability پنل، داده‌ی همین اپ را نشان بدهد.
        /// </summary>
        public string ServiceName { get; set; } = "";

        /// <summary>نسخه‌ی اپ؛ خالی = نسخه‌ی Assembly ورودی.</summary>
        public string? ServiceVersion { get; set; }

        /// <summary>محیط اجرا (deployment.environment)؛ خالی = ASPNETCORE_ENVIRONMENT.</summary>
        public string? Environment { get; set; }

        /// <summary>آدرس OTel Collector.</summary>
        public string OtlpEndpoint { get; set; } = "http://localhost:4317";

        public OtlpExportProtocol Protocol { get; set; } = OtlpExportProtocol.Grpc;

        public bool EnableMetrics { get; set; } = true;
        public bool EnableTracing { get; set; } = true;
        public bool EnableLogging { get; set; } = true;

        /// <summary>فاصله‌ی ارسال متریک (ثانیه). مقدار پیش‌فرض ۱۵ ثانیه است تا نمودارهای rate پنل دقیق باشند.</summary>
        public int MetricsExportIntervalSeconds { get; set; } = 15;

        /// <summary>نسبت نمونه‌برداری تریس (۰ تا ۱). در ترافیک سنگین مثلاً 0.1 بگذارید. تریس‌های خطادار از parent تبعیت می‌کنند.</summary>
        public double TraceSampleRatio { get; set; } = 1.0;

        /// <summary>ثبت instrumentation کوئری‌های SQL (Microsoft.Data.SqlClient / System.Data.SqlClient).</summary>
        public bool EnableSqlInstrumentation { get; set; } = true;

        /// <summary>
        /// ثبت متن کوئری SQL در تریس (پارامترها همیشه حذف می‌شوند). به‌صورت پیش‌فرض خاموش است چون متن کوئری
        /// می‌تواند ساختار داده‌ی حساس را نشان دهد؛ برای پیدا کردن کوئری‌های کند روشنش کنید.
        /// </summary>
        public bool RecordSqlStatements { get; set; } = false;

        /// <summary>مسیرهایی که trace نشوند (probe و health و ...).</summary>
        public List<string> IgnoredPaths { get; set; } = new List<string> { "/health", "/healthz", "/ksc/observability", "/metrics", "/_framework", "/_blazor", "/_content" };

        /// <summary>هدر <c>X-Trace-Id</c> روی پاسخ‌ها (برای ارجاع پشتیبانی به تریس).</summary>
        public bool EmitTraceIdHeader { get; set; } = true;

        /// <summary>Meterهای سفارشی اپ که باید export شوند.</summary>
        public List<string> AdditionalMeters { get; set; } = new List<string>();

        /// <summary>ActivitySourceهای سفارشی اپ که باید export شوند.</summary>
        public List<string> AdditionalActivitySources { get; set; } = new List<string>();

        /// <summary>وابستگی‌هایی که باید سلامتشان پایش شود (SQL، HTTP، TCP).</summary>
        public List<KscDependencyOptions> Dependencies { get; set; } = new List<KscDependencyOptions>();

        public string ResolveEnvironment() =>
            !string.IsNullOrWhiteSpace(Environment) ? Environment!
            : System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
              ?? System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
              ?? "Production";
    }
}
