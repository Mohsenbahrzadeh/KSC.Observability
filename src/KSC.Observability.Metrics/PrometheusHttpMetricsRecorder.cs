using System;
using KSC.Observability.Metrics.Internal;
using Prometheus;

namespace KSC.Observability.Metrics
{
    /// <summary>
    /// Records HTTP request metrics: a monotonic request counter, an in-flight gauge (the
    /// "how many requests are being processed right now" signal) and a latency histogram.
    /// </summary>
    public sealed class PrometheusHttpMetricsRecorder : IHttpMetricsRecorder
    {
        private readonly Counter _requestsTotal;
        private readonly Gauge _inFlight;
        private readonly Histogram _duration;
        private readonly bool _trackPath;

        public PrometheusHttpMetricsRecorder(IMetricFactory factory, ObservabilityOptions options)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (options == null) throw new ArgumentNullException(nameof(options));

            _trackPath = options.TrackRequestPath;
            string p = options.MetricPrefix;

            var counterLabels = _trackPath
                ? new[] { LabelNames.Method, LabelNames.StatusCode, LabelNames.Path }
                : new[] { LabelNames.Method, LabelNames.StatusCode };

            _requestsTotal = factory.CreateCounter(
                MetricNaming.Build(p, MetricSuffixes.HttpRequestsTotal),
                "Total number of HTTP requests processed.",
                new CounterConfiguration { LabelNames = counterLabels });

            _inFlight = factory.CreateGauge(
                MetricNaming.Build(p, MetricSuffixes.HttpRequestsInFlight),
                "Number of HTTP requests currently being processed.");

            _duration = factory.CreateHistogram(
                MetricNaming.Build(p, MetricSuffixes.HttpRequestDurationSeconds),
                "HTTP request duration in seconds.",
                new HistogramConfiguration
                {
                    Buckets = options.RequestDurationSecondsBuckets,
                    LabelNames = new[] { LabelNames.Method }
                });
        }

        public void RequestStarted()
        {
            _inFlight.Inc();
        }

        public void RequestCompleted(string method, string? path, int statusCode, double elapsedSeconds)
        {
            _inFlight.Dec();

            var verb = string.IsNullOrEmpty(method) ? "UNKNOWN" : method.ToUpperInvariant();
            var code = statusCode.ToString();

            if (_trackPath)
            {
                _requestsTotal.WithLabels(verb, code, path ?? "unknown").Inc();
            }
            else
            {
                _requestsTotal.WithLabels(verb, code).Inc();
            }

            if (elapsedSeconds < 0) elapsedSeconds = 0;
            _duration.WithLabels(verb).Observe(elapsedSeconds);
        }
    }
}
