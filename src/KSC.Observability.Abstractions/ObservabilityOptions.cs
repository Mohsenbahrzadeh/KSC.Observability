using System;

namespace KSC.Observability
{
    /// <summary>
    /// Runtime configuration for the observability pipeline. Every value has a sane default,
    /// so a zero-config installation still produces useful metrics.
    /// </summary>
    public sealed class ObservabilityOptions
    {
        /// <summary>Logical name of the application, emitted as the <c>service</c> label.</summary>
        public string ServiceName { get; set; } = "dotnet-app";

        /// <summary>Identifier of this running instance, emitted as the <c>instance</c> label.</summary>
        public string InstanceId { get; set; } = System.Environment.MachineName;

        /// <summary>Deployment environment (production, staging, ...), emitted as the <c>env</c> label.</summary>
        public string Environment { get; set; } = "production";

        /// <summary>Prefix applied to every metric name (e.g. <c>ksc_process_cpu_usage_percent</c>).</summary>
        public string MetricPrefix { get; set; } = "ksc";

        /// <summary>Absolute path that exposes the Prometheus scrape endpoint.</summary>
        public string MetricsPath { get; set; } = "/metrics";

        /// <summary>Collect process CPU, memory, GC, thread and uptime gauges.</summary>
        public bool EnableSystemMetrics { get; set; } = true;

        /// <summary>Collect per-request counters, in-flight gauge and a latency histogram.</summary>
        public bool EnableHttpMetrics { get; set; } = true;

        /// <summary>Track distinct active users (sessions) seen within <see cref="ActiveUserWindow"/>.</summary>
        public bool EnableActiveUserTracking { get; set; } = true;

        /// <summary>How often the background sampler refreshes system gauges.</summary>
        public TimeSpan SystemMetricsInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>A user is considered "active" if seen within this sliding window.</summary>
        public TimeSpan ActiveUserWindow { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// When <c>true</c>, the request path is added as a label. Off by default because raw
        /// paths can explode label cardinality; prefer a normalized route when you enable it.
        /// </summary>
        public bool TrackRequestPath { get; set; } = false;

        /// <summary>Histogram bucket boundaries (seconds) for request duration.</summary>
        public double[] RequestDurationSecondsBuckets { get; set; } = DefaultDurationBuckets();

        /// <summary>
        /// Optional bearer token. When set, scrapes must send
        /// <c>Authorization: Bearer &lt;token&gt;</c> to read the metrics endpoint.
        /// </summary>
        public string? MetricsAccessToken { get; set; }

        /// <summary>Throws if the options are internally inconsistent.</summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ServiceName))
                throw new InvalidOperationException("ObservabilityOptions.ServiceName must not be empty.");
            if (string.IsNullOrWhiteSpace(MetricsPath) || MetricsPath[0] != '/')
                throw new InvalidOperationException("ObservabilityOptions.MetricsPath must start with '/'.");
            if (string.IsNullOrWhiteSpace(MetricPrefix))
                throw new InvalidOperationException("ObservabilityOptions.MetricPrefix must not be empty.");
            if (SystemMetricsInterval <= TimeSpan.Zero)
                throw new InvalidOperationException("ObservabilityOptions.SystemMetricsInterval must be positive.");
            if (ActiveUserWindow <= TimeSpan.Zero)
                throw new InvalidOperationException("ObservabilityOptions.ActiveUserWindow must be positive.");
        }

        private static double[] DefaultDurationBuckets()
        {
            return new[] { 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10 };
        }
    }
}
