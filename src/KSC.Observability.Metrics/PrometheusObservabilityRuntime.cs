using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KSC.Observability.Metrics.Internal;
using Prometheus;

namespace KSC.Observability.Metrics
{
    /// <summary>
    /// Prometheus-backed implementation of <see cref="IObservabilityRuntime"/>. Owns an isolated
    /// <see cref="CollectorRegistry"/> so it never collides with any other prometheus-net usage in
    /// the host, wires the configured collectors, and serializes them on demand.
    /// </summary>
    public sealed class PrometheusObservabilityRuntime : IObservabilityRuntime
    {
        private readonly CollectorRegistry _registry;
        private readonly PrometheusSystemMetricsCollector? _systemCollector;
        private bool _disposed;

        public PrometheusObservabilityRuntime(ObservabilityOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Options.Validate();

            _registry = global::Prometheus.Metrics.NewCustomRegistry();
            _registry.SetStaticLabels(new Dictionary<string, string>
            {
                [LabelNames.Service] = options.ServiceName,
                [LabelNames.Instance] = options.InstanceId,
                [LabelNames.Environment] = options.Environment
            });

            var factory = global::Prometheus.Metrics.WithCustomRegistry(_registry);

            PublishBuildInfo(factory, options);

            Http = new PrometheusHttpMetricsRecorder(factory, options);
            Users = new PrometheusActiveUserTracker(factory, options);

            if (options.EnableSystemMetrics)
            {
                _systemCollector = new PrometheusSystemMetricsCollector(factory, options);
                _systemCollector.Start();
            }
        }

        public ObservabilityOptions Options { get; }

        public IHttpMetricsRecorder Http { get; }

        public IActiveUserTracker Users { get; }

        /// <summary>The underlying registry, exposed for advanced scenarios and testing.</summary>
        public CollectorRegistry Registry => _registry;

        public void WriteMetrics(Stream output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            // Metrics exposition is I/O-light; blocking here keeps the integration layer simple.
            _registry.CollectAndExportAsTextAsync(output).GetAwaiter().GetResult();
        }

        private static void PublishBuildInfo(IMetricFactory factory, ObservabilityOptions options)
        {
            var version = typeof(PrometheusObservabilityRuntime).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(PrometheusObservabilityRuntime).Assembly.GetName().Version?.ToString()
                ?? "unknown";

            var info = factory.CreateGauge(
                MetricNaming.Build(options.MetricPrefix, MetricSuffixes.BuildInfo),
                "Build information; always 1, with the library version as a label.",
                new GaugeConfiguration { LabelNames = new[] { "version" } });

            info.WithLabels(version).Set(1);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _systemCollector?.Dispose();
            (Users as IDisposable)?.Dispose();
        }
    }
}
