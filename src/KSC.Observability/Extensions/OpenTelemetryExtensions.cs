using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// متریک + تریس + لاگ را از طریق OTLP به Collector می‌فرستد، SQL/HttpClient را instrument می‌کند
    /// و وابستگی‌های تعریف‌شده را پایش می‌کند. اولویت: appsettings (<c>KscObservability</c>) سپس delegate.
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
        options.ServiceVersion = version;
        options.Environment = options.ResolveEnvironment();
        services.AddSingleton(options);

        var otel = services.AddOpenTelemetry().ConfigureResource(r => r
            .AddService(options.ServiceName, serviceVersion: version, serviceInstanceId: System.Environment.MachineName)
            .AddAttributes(new KeyValuePair<string, object>[]
            {
                new("deployment.environment", options.Environment),
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
                 .AddMeter(ActiveUsersMeter.Name, KscTelemetry.MeterName, KscTelemetry.BusinessMeterName);
                if (options.EnableSqlInstrumentation) m.AddSqlClientInstrumentation(); // db.client.*
                // مرزهای پیش‌فرض هیستوگرام برای میلی‌ثانیه است؛ این متریک به ثانیه ثبت می‌شود
                m.AddView(KscTelemetry.DependencyDurationName, new ExplicitBucketHistogramConfiguration { Boundaries = KscTelemetry.SecondsBuckets });
                foreach (var meter in options.AdditionalMeters) m.AddMeter(meter);
                m.AddOtlpExporter((o, reader) =>
                {
                    ConfigureExporter(o, options, "v1/metrics");
                    reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = Math.Max(1, options.MetricsExportIntervalSeconds) * 1000;
                });
            });
        }

        if (options.EnableTracing)
        {
            otel.WithTracing(t =>
            {
                t.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(Math.Clamp(options.TraceSampleRatio, 0, 1))))
                 .AddAspNetCoreInstrumentation(o =>
                 {
                     o.Filter = ctx => !options.IgnoredPaths.Any(p => ctx.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
                     o.RecordException = true;
                 })
                 .AddHttpClientInstrumentation(o => o.RecordException = true);
                if (options.EnableSqlInstrumentation)
                    t.AddSqlClientInstrumentation(o =>
                    {
                        o.RecordException = true;
                        if (!options.RecordSqlStatements)
                            o.EnrichWithSqlCommand = (activity, _) =>
                            {
                                activity.SetTag("db.query.text", null);
                                activity.SetTag("db.statement", null);
                            };
                    });
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

        if (options.Dependencies.Count > 0)
            services.AddHostedService<DependencyMonitorService>();

        return services;
    }

    /// <summary>
    /// میان‌افزار KSC: شمارش exceptionهای مدیریت‌نشده (بر اساس نوع و route) روی متریک و تریس، و هدر <c>X-Trace-Id</c>.
    /// باید <b>قبل</b> از میان‌افزارهای دیگر (و بعد از UseExceptionHandler) ثبت شود.
    /// </summary>
    public static IApplicationBuilder UseKscObservability(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<KscObservabilityOptions>();
        return app.Use(async (ctx, next) =>
        {
            if (options.EmitTraceIdHeader)
            {
                var traceId = Activity.Current?.TraceId.ToString();
                if (traceId is not null)
                    ctx.Response.OnStarting(() => { ctx.Response.Headers["X-Trace-Id"] = traceId; return Task.CompletedTask; });
            }
            try { await next(); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var route = (ctx.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
                KscTelemetry.RecordUnhandledException(ex, route);
                throw;
            }
        });
    }

    /// <summary>
    /// دو endpoint سبک (بدون احراز هویت):
    /// <c>/ksc/observability/info</c> (نام، نسخه، محیط، uptime، وضعیت وابستگی‌ها) و
    /// <c>/ksc/observability/health</c> (200 یا 503 بر اساس وابستگی‌های Critical، مناسب Load-Balancer).
    /// </summary>
    public static IEndpointRouteBuilder MapKscObservability(this IEndpointRouteBuilder endpoints, string prefix = "/ksc/observability")
    {
        endpoints.MapGet(prefix + "/info", (KscObservabilityOptions options) => Results.Json(new
        {
            service = options.ServiceName,
            version = options.ServiceVersion,
            environment = options.Environment,
            host = System.Environment.MachineName,
            startedAtUtc = StartedAtUtc,
            uptimeSeconds = (long)(DateTime.UtcNow - StartedAtUtc).TotalSeconds,
            dependencies = KscTelemetry.GetDependencyStatuses().Select(d => new
            {
                d.Name, type = d.Type.ToString(), d.Critical, d.Healthy, d.LatencyMs, d.Error, d.CheckedAtUtc,
            }),
        })).AllowAnonymous();

        endpoints.MapGet(prefix + "/health", () =>
        {
            var deps = KscTelemetry.GetDependencyStatuses();
            var down = deps.Where(d => d.Critical && !d.Healthy).Select(d => d.Name).ToArray();
            return Results.Json(new { status = down.Length == 0 ? "Healthy" : "Unhealthy", failing = down },
                statusCode: down.Length == 0 ? 200 : 503);
        }).AllowAnonymous();
        return endpoints;
    }

    /// <summary>نام قدیمی (سازگاری).</summary>
    public static IEndpointRouteBuilder MapKscObservabilityInfo(this IEndpointRouteBuilder endpoints, string pattern = "/ksc/observability/info") =>
        endpoints.MapKscObservability(pattern.EndsWith("/info") ? pattern[..^5] : pattern);

    private static void ConfigureExporter(OtlpExporterOptions o, KscObservabilityOptions options, string httpSignalPath)
    {
        o.Protocol = options.Protocol;
        // در HTTP/protobuf آدرس باید مسیر signal را داشته باشد؛ در gRPC نه.
        o.Endpoint = options.Protocol == OtlpExportProtocol.HttpProtobuf
            ? new Uri($"{options.OtlpEndpoint.TrimEnd('/')}/{httpSignalPath}")
            : new Uri(options.OtlpEndpoint);
    }
}

/// <summary>سرویس پس‌زمینه‌ی probe وابستگی‌ها.</summary>
internal sealed class DependencyMonitorService : BackgroundService
{
    private readonly KscObservabilityOptions _options;
    public DependencyMonitorService(KscObservabilityOptions options) => _options = options;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(_options.Dependencies.Select(d => Loop(d, stoppingToken)));

    private static async Task Loop(KscDependencyOptions dep, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await DependencyChecker.CheckAsync(dep, ct);
            try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, dep.IntervalSeconds)), ct); }
            catch (OperationCanceledException) { return; }
        }
    }
}
