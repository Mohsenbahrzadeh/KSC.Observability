using System;

namespace KSC.Observability
{
    /// <summary>
    /// Samples process-level resource metrics (CPU, memory, GC, threads, uptime) on a
    /// background interval and publishes them to the metric backend.
    /// </summary>
    public interface ISystemMetricsCollector : IDisposable
    {
        /// <summary>Begins periodic sampling. Safe to call more than once.</summary>
        void Start();

        /// <summary>Stops periodic sampling without disposing the underlying metrics.</summary>
        void Stop();
    }
}
