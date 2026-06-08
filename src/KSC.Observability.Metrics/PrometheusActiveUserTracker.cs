using System;
using System.Collections.Concurrent;
using System.Threading;
using KSC.Observability.Metrics.Internal;
using Prometheus;

namespace KSC.Observability.Metrics
{
    /// <summary>
    /// Counts distinct users that are concurrently using the application. Each call to
    /// <see cref="Touch"/> stamps a user key with the current time; a background timer prunes
    /// keys older than the configured window and publishes the surviving count as a gauge.
    /// </summary>
    public sealed class PrometheusActiveUserTracker : IActiveUserTracker
    {
        private readonly ConcurrentDictionary<string, long> _lastSeenTicks =
            new ConcurrentDictionary<string, long>(StringComparer.Ordinal);

        private readonly Gauge _activeUsers;
        private readonly long _windowTicks;
        private readonly object _gate = new object();
        private Timer? _timer;
        private bool _disposed;

        public PrometheusActiveUserTracker(IMetricFactory factory, ObservabilityOptions options)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (options == null) throw new ArgumentNullException(nameof(options));

            _windowTicks = options.ActiveUserWindow.Ticks;
            _activeUsers = factory.CreateGauge(
                MetricNaming.Build(options.MetricPrefix, MetricSuffixes.ActiveUsers),
                "Number of distinct users active within the configured sliding window.");

            // Sweep at a quarter of the window (bounded to a sane range) so the gauge stays fresh.
            var sweep = TimeSpan.FromTicks(Math.Max(TimeSpan.FromSeconds(5).Ticks, _windowTicks / 4));
            _timer = new Timer(OnSweep, null, sweep, sweep);
        }

        public int CurrentCount => _lastSeenTicks.Count;

        public void Touch(string userKey)
        {
            if (string.IsNullOrEmpty(userKey) || _disposed) return;
            _lastSeenTicks[userKey] = DateTime.UtcNow.Ticks;
        }

        private void OnSweep(object? state)
        {
            try
            {
                Prune();
            }
            catch
            {
                // Never let a background sweep crash the host.
            }
        }

        /// <summary>Removes stale keys and republishes the gauge. Exposed for deterministic tests.</summary>
        public void Prune()
        {
            var cutoff = DateTime.UtcNow.Ticks - _windowTicks;
            foreach (var pair in _lastSeenTicks)
            {
                if (pair.Value < cutoff)
                {
                    _lastSeenTicks.TryRemove(pair.Key, out _);
                }
            }
            _activeUsers.Set(_lastSeenTicks.Count);
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
            _lastSeenTicks.Clear();
        }
    }
}
