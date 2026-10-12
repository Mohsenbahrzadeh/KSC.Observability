using OpenTelemetry.Exporter;

namespace KSC.Observability;

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

    /// <summary>مسیرهایی که trace نشوند (probe و health و ...).</summary>
    public List<string> IgnoredPaths { get; set; } = new() { "/health", "/healthz", "/metrics", "/_framework", "/_blazor", "/_content" };

    /// <summary>Meterهای سفارشی اپ که باید export شوند.</summary>
    public List<string> AdditionalMeters { get; set; } = new();

    /// <summary>ActivitySourceهای سفارشی اپ که باید export شوند.</summary>
    public List<string> AdditionalActivitySources { get; set; } = new();
}
