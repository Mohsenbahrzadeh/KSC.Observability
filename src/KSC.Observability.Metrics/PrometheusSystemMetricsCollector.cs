using System;
using System.Diagnostics;
using System.Threading;
using KSC.Observability.Metrics.Internal;
using Prometheus;

namespace KSC.Observability.Metrics
{
    /// <summary>
    /// Samples process resource usage (CPU, memory, GC, threads, handles, uptime) on a background
    /// timer. CPU percentage is derived from the delta of <see cref="Process.TotalProcessorTime"/>
    /// over wall-clock time, which avoids the fragile performance-counter instance naming on IIS.
    /// </summary>
    public sealed class PrometheusSystemMetricsCollector : ISystemMetricsCollector
    {
        private readonly ObservabilityOptions _options;
        private readonly Process _process;
        private readonly object _gate = new object();

        private readonly Gauge _cpu;
        private readonly Gauge _workingSet;
        private readonly Gauge _privateMemory;
        private readonly Gauge _managedMemory;
        private readonly Gauge _threads;
        private readonly Gauge _handles;
        private readonly Gauge _uptime;
        private readonly Counter _gcCollections;

        private readonly DateTime _startedUtc;
        private DateTime _lastSampleUtc;
        private TimeSpan _lastCpuTime;
        private Timer? _timer;
        private bool _disposed;

        public PrometheusSystemMetricsCollector(IMetricFactory factory, ObservabilityOptions options)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _process = Process.GetCurrentProcess();

            string p = options.MetricPrefix;
            _cpu = factory.CreateGauge(
                MetricNaming.Build(p, MetricSuffixes.ProcessCpuUsagePercent),
                "Process CPU usage as a percentage of one logical core (can exceed 100 on multi-core).");
            _workingSet = factory.CreateGauge(
                MetricNaming.Build(p, MetricSuffixes.ProcessWorkingSetBytes),
                "Process working set (physical memory in use) in bytes.");
            _privateMemory = factory.CreateGauge(
                MetricNaming.Build(p, MetricSuffixes.ProcessPrivateMemoryBytes),
                "Process private memory in bytes.");
            _managedMemory = factory.CreateGauge(
                MetricNaming.Build(p, MetricSuffixes.ProcessManagedMemoryBytes),
                "Bytes currently allocated on the managed GC heap.");
            _threads = factory.CreateGauge(
                MetricNaming.Build(p, MetricSuffixes.ProcessThreads),
                "Number of OS threads in the process.");
            _handles = factory.CreateGauge(
                MetricNaming.Build(p, MetricSuffixes.ProcessHandles),
                "Number of operating system handles held by the process.");
            _uptime = factory.CreateGauge(
                MetricNaming.Build(p, MetricSuffixes.ProcessUptimeSeconds),
                "Seconds since the process started.");
            _gcCollections = factory.CreateCounter(
                MetricNaming.Build(p, MetricSuffixes.GcCollectionsTotal),
                "Total number of garbage collections, by generation.",
                new CounterConfiguration { LabelNames = new[] { LabelNames.Generation } });

            _startedUtc = TryGetStartTimeUtc();
            _lastSampleUtc = DateTime.UtcNow;
            _lastCpuTime = SafeTotalProcessorTime();
        }

        public void Start()
        {
            lock (_gate)
            {
                if (_disposed || _timer != null) return;
                // Prime an initial sample immediately, then on the configured interval.
                _timer = new Timer(OnTick, null, TimeSpan.Zero, _options.SystemMetricsInterval);
            }
        }

        public void Stop()
        {
            lock (_gate)
            {
                _timer?.Dispose();
                _timer = null;
            }
        }

        private void OnTick(object? state)
        {
            try
            {
                Sample();
            }
            catch
            {
                // A failing sample must never bring down the host application.
            }
        }

        private void Sample()
        {
            _process.Refresh();

            var nowUtc = DateTime.UtcNow;
            var cpuNow = SafeTotalProcessorTime();
            var wallSeconds = (nowUtc - _lastSampleUtc).TotalSeconds;
            if (wallSeconds > 0)
            {
                var cpuSeconds = (cpuNow - _lastCpuTime).TotalSeconds;
                var cores = Math.Max(1, System.Environment.ProcessorCount);
                var percent = cpuSeconds / (wallSeconds * cores) * 100.0;
                if (percent < 0) percent = 0;
                _cpu.Set(Math.Round(percent, 2));
            }
            _lastSampleUtc = nowUtc;
            _lastCpuTime = cpuNow;

            _workingSet.Set(_process.WorkingSet64);
            _privateMemory.Set(_process.PrivateMemorySize64);
            _managedMemory.Set(GC.GetTotalMemory(forceFullCollection: false));
            _threads.Set(_process.Threads.Count);
            _handles.Set(_process.HandleCount);
            _uptime.Set((nowUtc - _startedUtc).TotalSeconds);

            int generations = GC.MaxGeneration;
            for (int gen = 0; gen <= generations; gen++)
            {
                _gcCollections.WithLabels(gen.ToString()).IncTo(GC.CollectionCount(gen));
            }
        }

        private TimeSpan SafeTotalProcessorTime()
        {
            try { return _process.TotalProcessorTime; }
            catch { return TimeSpan.Zero; }
        }

        private DateTime TryGetStartTimeUtc()
        {
            try { return _process.StartTime.ToUniversalTime(); }
            catch { return DateTime.UtcNow; }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _timer?.Dispose();
                _timer = null;
            }
            _process.Dispose();
        }
    }
}
