using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace KSC.Observability
{
    /// <summary>
    /// ابزارهای (instrument) استاندارد KSC. نام‌ها با قرارداد OTel→Prometheus تبدیل می‌شوند:
    /// <c>ksc.app.start_time</c> → <c>ksc_app_start_time_seconds</c> و ...
    /// پنل KSC.Authorization همین نام‌ها را می‌خواند؛ تغییرشان داشبورد را می‌شکند.
    /// </summary>
    public static class KscTelemetry
    {
        public const string MeterName = "KSC.Observability";
        public const string BusinessMeterName = "KSC.Business";

        internal static readonly Meter Meter = new Meter(MeterName, "1.0.0");
        internal static readonly Meter Business = new Meter(BusinessMeterName, "1.0.0");

        private static readonly double StartUnix = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        // ─── uptime / restart ───────────────────────────────────────────────
        // مقدار ثابت در طول عمر پروسه؛ تغییرش بین دو نمونه = restart (changes() در پنل).
        internal static readonly ObservableGauge<double> StartTime =
            Meter.CreateObservableGauge("ksc.app.start_time", () => StartUnix, "s", "Process start time (unix seconds)");

        // ─── خطاهای مدیریت‌نشده ─────────────────────────────────────────────
        internal static readonly Counter<long> UnhandledExceptions =
            Meter.CreateCounter<long>("ksc.http.unhandled_exceptions", "{exception}", "Unhandled exceptions by type and route");

        // ─── سلامت وابستگی‌ها ───────────────────────────────────────────────
        internal const string DependencyDurationName = "ksc.dependency.check.duration";

        /// <summary>مرزهای باکت برای هیستوگرام‌های ثانیه‌ای (همان مقادیر http.server.request.duration).</summary>
        internal static readonly double[] SecondsBuckets = { 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 };

        internal static readonly Histogram<double> DependencyDuration =
            Meter.CreateHistogram<double>(DependencyDurationName, "s", "Duration of dependency health probe");

        internal static readonly Counter<long> DependencyFailures =
            Meter.CreateCounter<long>("ksc.dependency.check.failures", "{check}", "Failed dependency health probes");

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, DependencyStatus> Statuses = new Dictionary<string, DependencyStatus>();

        internal static readonly ObservableGauge<int> DependencyUp =
            Meter.CreateObservableGauge("ksc.dependency.up", ObserveDependencies, "{state}", "1 = dependency healthy, 0 = unhealthy");

        private static IEnumerable<Measurement<int>> ObserveDependencies()
        {
            lock (Gate)
            {
                var list = new List<Measurement<int>>(Statuses.Count);
                foreach (var s in Statuses.Values)
                    list.Add(new Measurement<int>(s.Healthy ? 1 : 0,
                        new KeyValuePair<string, object?>("dependency", s.Name),
                        new KeyValuePair<string, object?>("dependency_type", s.Type.ToString().ToLowerInvariant())));
                return list;
            }
        }

        internal static void SetStatus(DependencyStatus status)
        {
            lock (Gate) Statuses[status.Name] = status;
        }

        /// <summary>آخرین وضعیت وابستگی‌ها (برای endpoint سلامت).</summary>
        public static IReadOnlyList<DependencyStatus> GetDependencyStatuses()
        {
            lock (Gate) return new List<DependencyStatus>(Statuses.Values);
        }

        internal static void RecordUnhandledException(Exception ex, string? route)
        {
            UnhandledExceptions.Add(1,
                new KeyValuePair<string, object?>("exception.type", ex.GetType().Name),
                new KeyValuePair<string, object?>("http.route", string.IsNullOrEmpty(route) ? "(unknown)" : route));
            var activity = Activity.Current;
            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                var tags = new ActivityTagsCollection
                {
                    { "exception.type", ex.GetType().FullName },
                    { "exception.message", ex.Message },
                    { "exception.stacktrace", ex.ToString() },
                };
                activity.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, tags));
            }
        }

        // ─── متریک‌های کسب‌وکاری (API ساده برای برنامه‌نویس) ────────────────
        /// <summary>شمارنده‌ی کسب‌وکاری؛ مثلاً <c>KscTelemetry.Counter("orders.created")</c>.</summary>
        public static Counter<long> Counter(string name, string? unit = null, string? description = null) =>
            Business.CreateCounter<long>(name, unit, description);

        /// <summary>هیستوگرام کسب‌وکاری؛ مثلاً زمان پردازش سفارش.</summary>
        public static Histogram<double> Histogram(string name, string? unit = null, string? description = null) =>
            Business.CreateHistogram<double>(name, unit, description);

        /// <summary>گیج مشاهده‌پذیر؛ مثلاً تعداد آیتم‌های صف.</summary>
        public static ObservableGauge<T> Gauge<T>(string name, Func<T> observe, string? unit = null, string? description = null) where T : struct =>
            Business.CreateObservableGauge(name, observe, unit, description);
    }

    public sealed class DependencyStatus
    {
        public string Name { get; set; } = "";
        public KscDependencyType Type { get; set; }
        public bool Critical { get; set; }
        public bool Healthy { get; set; }
        public double LatencyMs { get; set; }
        public string? Error { get; set; }
        public DateTime CheckedAtUtc { get; set; }
    }
}
