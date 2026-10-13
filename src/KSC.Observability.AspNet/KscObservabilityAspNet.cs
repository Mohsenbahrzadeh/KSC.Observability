using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using KSC.Observability;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

[assembly: PreApplicationStartMethod(typeof(KscObservabilityBootstrap), nameof(KscObservabilityBootstrap.PreStart))]

namespace KSC.Observability
{
    /// <summary>
    /// راه‌اندازی Observability برای ASP.NET (.NET Framework 4.8): MVC / Web API / Web Forms.
    /// <code>
    /// // Global.asax.cs → Application_Start
    /// KscObservability.Start(o =&gt; o.ServiceName = "my-client-id");
    /// </code>
    /// یا بدون کد: کلیدهای <c>Ksc:Observability:ServiceName</c> و <c>Ksc:Observability:OtlpEndpoint</c> را در appSettings بگذارید
    /// (راه‌اندازی خودکار هنگام PreApplicationStart).
    /// </summary>
    public static class KscObservability
    {
        private static readonly object Gate = new object();
        private static IDisposable? _handle;
        private static KscObservabilityOptions? _options;
        private static CancellationTokenSource? _cts;

        public static KscObservabilityOptions? Options => _options;

        public static IDisposable Start(Action<KscObservabilityOptions>? configure = null)
        {
            lock (Gate)
            {
                if (_handle != null) return _handle;

                var o = LoadFromAppSettings();
                configure?.Invoke(o);
                if (string.IsNullOrWhiteSpace(o.ServiceName))
                    throw new InvalidOperationException("KscObservability: ServiceName تنظیم نشده (باید ClientId اپ در پنل باشد).");

                var entry = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                o.ServiceVersion = o.ServiceVersion ?? HttpRuntimeVersion() ?? "1.0.0";
                o.Environment = o.ResolveEnvironment();
                _options = o;

                Action<ResourceBuilder> res = r => r
                    .AddService(o.ServiceName, serviceVersion: o.ServiceVersion, serviceInstanceId: Environment.MachineName)
                    .AddAttributes(new[]
                    {
                        new KeyValuePair<string, object>("deployment.environment", o.Environment),
                        new KeyValuePair<string, object>("host.name", Environment.MachineName),
                    });

                var disposables = new List<IDisposable>();

                if (o.EnableMetrics)
                {
                    var mb = Sdk.CreateMeterProviderBuilder()
                        .ConfigureResource(res)
                        .AddAspNetInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation()
                        .AddProcessInstrumentation()
                        .AddMeter(KscTelemetry.MeterName, KscTelemetry.BusinessMeterName);
                    if (o.EnableSqlInstrumentation) mb.AddSqlClientInstrumentation();
                    foreach (var m in o.AdditionalMeters) mb.AddMeter(m);
                    mb.AddOtlpExporter((e, reader) =>
                    {
                        Configure(e, o, "v1/metrics");
                        reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = Math.Max(1, o.MetricsExportIntervalSeconds) * 1000;
                    });
                    disposables.Add(mb.Build());
                }

                if (o.EnableTracing)
                {
                    var tb = Sdk.CreateTracerProviderBuilder()
                        .ConfigureResource(res)
                        .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(Math.Max(0, Math.Min(1, o.TraceSampleRatio)))))
                        .AddAspNetInstrumentation(a =>
                        {
                            a.Filter = ctx => !o.IgnoredPaths.Any(p => ctx.Request.Path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
                            a.RecordException = true;
                        })
                        .AddHttpClientInstrumentation();
                    if (o.EnableSqlInstrumentation)
                    {
                        tb.AddSqlClientInstrumentation();
                        if (!o.RecordSqlStatements) tb.AddProcessor(new SqlScrubProcessor());
                    }
                    foreach (var s in o.AdditionalActivitySources) tb.AddSource(s);
                    tb.AddOtlpExporter(e => Configure(e, o, "v1/traces"));
                    disposables.Add(tb.Build());
                }

                if (o.Dependencies.Count > 0)
                {
                    _cts = new CancellationTokenSource();
                    foreach (var d in o.Dependencies) _ = LoopAsync(d, _cts.Token);
                }

                _handle = new Handle(() =>
                {
                    _cts?.Cancel();
                    foreach (var d in disposables) d.Dispose();
                    lock (Gate) _handle = null;
                });
                return _handle;
            }
        }

        /// <summary>در <c>Application_Error</c> صدا بزنید (یا ماژول خودکار این کار را می‌کند).</summary>
        public static void RecordException(Exception ex, string? route = null) => KscTelemetry.RecordUnhandledException(ex, route);

        public static void Stop() => _handle?.Dispose();

        private static async Task LoopAsync(KscDependencyOptions dep, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await DependencyChecker.CheckAsync(dep, ct).ConfigureAwait(false);
                try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, dep.IntervalSeconds)), ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }

        private static void Configure(OtlpExporterOptions e, KscObservabilityOptions o, string signalPath)
        {
            e.Protocol = o.Protocol;
            e.Endpoint = o.Protocol == OtlpExportProtocol.HttpProtobuf
                ? new Uri(o.OtlpEndpoint.TrimEnd('/') + "/" + signalPath)
                : new Uri(o.OtlpEndpoint);
        }

        private static string? HttpRuntimeVersion()
        {
            try
            {
                var asm = HttpContext.Current?.ApplicationInstance?.GetType().BaseType?.Assembly;
                return asm?.GetName().Version?.ToString();
            }
            catch { return null; }
        }

        internal static KscObservabilityOptions LoadFromAppSettings()
        {
            var s = ConfigurationManager.AppSettings;
            var o = new KscObservabilityOptions();
            string? V(string k) => s["Ksc:Observability:" + k];
            if (V("ServiceName") is { Length: > 0 } n) o.ServiceName = n;
            if (V("OtlpEndpoint") is { Length: > 0 } ep) o.OtlpEndpoint = ep;
            if (V("ServiceVersion") is { Length: > 0 } v) o.ServiceVersion = v;
            if (V("Environment") is { Length: > 0 } env) o.Environment = env;
            if (V("TraceSampleRatio") is { Length: > 0 } r && double.TryParse(r, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ratio)) o.TraceSampleRatio = ratio;
            if (V("Protocol") is { Length: > 0 } p && p.Equals("http", StringComparison.OrdinalIgnoreCase)) o.Protocol = OtlpExportProtocol.HttpProtobuf;
            return o;
        }

        private sealed class Handle : IDisposable
        {
            private readonly Action _dispose;
            public Handle(Action dispose) => _dispose = dispose;
            public void Dispose() => _dispose();
        }
    }

    /// <summary>متن کوئری SQL را از تریس حذف می‌کند (وقتی RecordSqlStatements=false).</summary>
    internal sealed class SqlScrubProcessor : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data)
        {
            if (data.GetTagItem("db.system") == null && data.GetTagItem("db.system.name") == null) return;
            data.SetTag("db.statement", null);
            data.SetTag("db.query.text", null);
        }
    }

    /// <summary>ماژول HTTP: ثبت exceptionهای مدیریت‌نشده و هدر X-Trace-Id.</summary>
    public sealed class KscObservabilityModule : IHttpModule
    {
        public void Init(HttpApplication app)
        {
            app.Error += (s, _) =>
            {
                var a = (HttpApplication)s;
                var ex = a.Server.GetLastError();
                if (ex == null) return;
                if (ex is HttpUnhandledException && ex.InnerException != null) ex = ex.InnerException;
                KscObservability.RecordException(ex, a.Request.Path);
            };
            app.PreSendRequestHeaders += (s, _) =>
            {
                var a = (HttpApplication)s;
                if (KscObservability.Options?.EmitTraceIdHeader != true) return;
                var id = Activity.Current?.TraceId.ToString();
                if (id != null && a.Response.Headers["X-Trace-Id"] == null) a.Response.Headers["X-Trace-Id"] = id;
            };
        }

        public void Dispose() { }
    }

    public static class KscObservabilityBootstrap
    {
        public static void PreStart()
        {
            var cfg = KscObservability.LoadFromAppSettings();
            if (string.IsNullOrWhiteSpace(cfg.ServiceName)) return; // بدون تنظیم appSettings، راه‌اندازی دستی (Start) لازم است

            DynamicModuleUtility.RegisterModule(typeof(OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule));
            DynamicModuleUtility.RegisterModule(typeof(KscObservabilityModule));
            KscObservability.Start();
        }
    }
}
