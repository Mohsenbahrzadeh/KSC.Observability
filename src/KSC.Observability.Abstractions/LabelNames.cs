namespace KSC.Observability
{
    /// <summary>Canonical Prometheus label names used across all metrics.</summary>
    public static class LabelNames
    {
        public const string Service = "service";
        public const string Instance = "instance";
        public const string Environment = "env";
        public const string Method = "method";
        public const string StatusCode = "code";
        public const string Path = "path";
        public const string Generation = "generation";
    }

    /// <summary>
    /// Metric name suffixes (without the configurable prefix). The runtime composes the
    /// final name as <c>{prefix}_{suffix}</c>, e.g. <c>ksc_http_requests_total</c>.
    /// </summary>
    public static class MetricSuffixes
    {
        // System
        public const string ProcessCpuUsagePercent = "process_cpu_usage_percent";
        public const string ProcessWorkingSetBytes = "process_working_set_bytes";
        public const string ProcessPrivateMemoryBytes = "process_private_memory_bytes";
        public const string ProcessManagedMemoryBytes = "process_managed_memory_bytes";
        public const string ProcessThreads = "process_threads";
        public const string ProcessHandles = "process_handles";
        public const string ProcessUptimeSeconds = "process_uptime_seconds";
        public const string GcCollectionsTotal = "gc_collections_total";

        // HTTP
        public const string HttpRequestsTotal = "http_requests_total";
        public const string HttpRequestsInFlight = "http_requests_in_flight";
        public const string HttpRequestDurationSeconds = "http_request_duration_seconds";

        // Users
        public const string ActiveUsers = "active_users";

        // Meta
        public const string BuildInfo = "build_info";
    }
}
