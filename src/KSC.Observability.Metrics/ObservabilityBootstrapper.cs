using System;

namespace KSC.Observability.Metrics
{
    /// <summary>
    /// Builds a <see cref="PrometheusObservabilityRuntime"/> from options and installs it as the
    /// process-wide runtime. Idempotent: the first successful call wins, later calls are no-ops.
    /// </summary>
    public static class ObservabilityBootstrapper
    {
        private static readonly object Gate = new object();

        /// <summary>
        /// Initializes the runtime with the supplied options if it has not been initialized yet.
        /// Returns the active runtime either way.
        /// </summary>
        public static IObservabilityRuntime Initialize(ObservabilityOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var existing = ObservabilityHost.TryGet();
            if (existing != null) return existing;

            lock (Gate)
            {
                existing = ObservabilityHost.TryGet();
                if (existing != null) return existing;

                var runtime = new PrometheusObservabilityRuntime(options);
                ObservabilityHost.SetRuntime(runtime);
                return ObservabilityHost.Current;
            }
        }

        /// <summary>Initializes the runtime with default options.</summary>
        public static IObservabilityRuntime Initialize()
        {
            return Initialize(new ObservabilityOptions());
        }
    }
}
