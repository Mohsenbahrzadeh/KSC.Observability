using System.Diagnostics.Metrics;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace KSC.Observability.Extensions;

public static class OpenTelemetryExtensions
{
    private static readonly DateTime StartedAtUtc = DateTime.UtcNow;

    // meter برای custom metrics (مثل تعداد کاربران فعال)
    private static readonly Meter ActiveUsersMeter = new("KSC.ActiveUsers", "1.0.0");
    public static Gauge<int> ActiveUsersGauge { get; } =
        ActiveUsersMeter.CreateGauge<int>("active_users", "count", "Number of active users");

    // ─── API قدیمی (سازگار با نسخه‌ی قبلی) ────────────────────────────────────
    public static IHostBuilder AddKscObservability(this IHostBuilder hostBuilder, string serviceName, string otlpEndpoint = "http://localhost:4317")
    {
        hostBuilder.ConfigureServices((ctx, services) =>
            services.AddKscObservability(ctx.Configuration, o =>
            {
                o.ServiceName = serviceName;
                o.OtlpEndpoint = otlpEndpoint;
            }));
        return hostBuilder;
    }

    // ─── API جدید: builder.AddKscObservability(o => o.ServiceName = "my-client-id") ──
    public static TBuilder AddKscObservability<TBuilder>(this TBuilder builder, Action<KscObservabilityOptions>? configure = null)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddKscObservability(builder.Configuration, configure);
        return builder;
    }

    /// <summary>
    /// متریک + تریس + لاگ را از طریق OTLP به Collector می‌فرستد.
    /// اولویت: appsettings (<c>KscObservability</c>) سپس delegate.
    /// </summary>
    public static IServiceCollection AddKscObservability(this IServiceCollection services, IConfiguration? configuration, Action<KscObservabilityOptions>? configure = null)
    {
        var options = new KscObservabilityOptions();
        configuration?.GetSection(KscObservabilityOptions.SectionName).Bind(options);
        configure?.Invoke(options);

        if (string.IsNullOrWhiteSpace(options.ServiceName))
            throw new InvalidOperationException(
                "KscObservability: ServiceName تنظیم نشده. مقدار آن باید ClientId اپ در پنل KSC.Authorization باشد.");

        var entry = Assembly.GetEntryAssembly();
        var version = options.ServiceVersion
            ?? entry?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
            ?? entry?.GetName().Version?.ToString()
            ?? "1.0.0";
        var environment = options.Environment
            ?? System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";

        options.ServiceVersion ??= version;
        options.Environment ??= environment;
        services.AddSingleton(options);

        var otel = services.AddOpenTelemetry().ConfigureResource(r => r
            .AddService(options.ServiceName, serviceVersion: version, serviceInstanceId: System.Environment.MachineName)
            .AddAttributes(new KeyValuePair<string, object>[]
            {
                new("deployment.environment", environment),
                new("host.name", System.Environment.MachineName),
            }));

        if (options.EnableMetrics)
        {
            otel.WithMetrics(m =>
            {
                m.AddRuntimeInstrumentation()       // GC، thread pool، exceptions، JIT
                 .AddProcessInstrumentation()       // CPU، memory
                 .AddAspNetCoreInstrumentation()    // http.server.*
                 .AddHttpClientInstrumentation()    // http.client.*
                 .AddMeter(ActiveUsersMeter.Name);
                foreach (var meter in options.AdditionalMeters) m.AddMeter(meter);
                m.AddOtlpExporter(o => ConfigureExporter(o, options, "v1/metrics"));
            });
        }

        if (options.EnableTracing)
        {
            otel.WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation(o =>
                 {
                     o.Filter = ctx => !options.IgnoredPaths.Any(p => ctx.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
                     o.RecordException = true;
                 })
                 .AddHttpClientInstrumentation(o => o.RecordException = true);
                foreach (var src in options.AdditionalActivitySources) t.AddSource(src);
                t.AddOtlpExporter(o => ConfigureExporter(o, options, "v1/traces"));
            });
        }

        if (options.EnableLogging)
        {
            otel.WithLogging(
                l => l.AddOtlpExporter(o => ConfigureExporter(o, options, "v1/logs")),
                o => { o.IncludeFormattedMessage = true; o.IncludeScopes = true; });
        }

        return services;
    }

    /// <summary>
    /// endpoint سبک <c>/ksc/observability/info</c> (نام، نسخه، محیط، uptime). پنل KSC.Authorization از آن
    /// برای نمایش وضعیت «زنده بودن» اپ استفاده می‌کند.
    /// </summary>
    public static IEndpointRouteBuilder MapKscObservabilityInfo(this IEndpointRouteBuilder endpoints, string pattern = "/ksc/observability/info")
    {
        endpoints.MapGet(pattern, (KscObservabilityOptions options) => Results.Json(new
        {
            service = options.ServiceName,
            version = options.ServiceVersion,
            environment = options.Environment,
            host = System.Environment.MachineName,
            startedAtUtc = StartedAtUtc,
            uptimeSeconds = (long)(DateTime.UtcNow - StartedAtUtc).TotalSeconds,
        })).AllowAnonymous();
        return endpoints;
    }

    private static void ConfigureExporter(OtlpExporterOptions o, KscObservabilityOptions options, string httpSignalPath)
    {
        o.Protocol = options.Protocol;
        // در HTTP/protobuf آدرس باید مسیر signal را داشته باشد؛ در gRPC نه.
        o.Endpoint = options.Protocol == OtlpExportProtocol.HttpProtobuf
            ? new Uri($"{options.OtlpEndpoint.TrimEnd('/')}/{httpSignalPath}")
            : new Uri(options.OtlpEndpoint);
    }
}
