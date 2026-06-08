using System;
using System.IO;

namespace KSC.Observability
{
    /// <summary>
    /// The composition root for a running observability pipeline. Implemented by the metrics
    /// backend (e.g. the Prometheus implementation) and consumed by integration layers, which
    /// stay decoupled from the concrete backend.
    /// </summary>
    public interface IObservabilityRuntime : IDisposable
    {
        /// <summary>Effective options this runtime was built with.</summary>
        ObservabilityOptions Options { get; }

        /// <summary>Recorder for HTTP request metrics.</summary>
        IHttpMetricsRecorder Http { get; }

        /// <summary>Tracker for concurrent active users.</summary>
        IActiveUserTracker Users { get; }

        /// <summary>
        /// Serializes the current metric values into <paramref name="output"/> using the
        /// Prometheus text exposition format.
        /// </summary>
        void WriteMetrics(Stream output);
    }
}
