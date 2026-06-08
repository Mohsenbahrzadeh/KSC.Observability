using System;
using KSC.Observability.Metrics;

namespace KSC.Observability.AspNet
{
    /// <summary>
    /// Public entry point for ASP.NET applications. Call <see cref="Initialize"/> from
    /// <c>Application_Start</c> to customize options; if you do nothing, the HttpModule
    /// auto-initializes from web.config (or defaults) on the first request.
    /// </summary>
    public static class KscObservability
    {
        /// <summary>
        /// Initializes the observability runtime. Options are first bound from web.config
        /// <c>&lt;appSettings&gt;</c>, then the optional <paramref name="configure"/> callback can
        /// override them in code. Safe to call multiple times; only the first call takes effect.
        /// </summary>
        public static IObservabilityRuntime Initialize(Action<ObservabilityOptions>? configure = null)
        {
            var options = AppSettingsOptionsBinder.Bind(new ObservabilityOptions());
            configure?.Invoke(options);
            return ObservabilityBootstrapper.Initialize(options);
        }

        /// <summary>Returns the active runtime, initializing it from config/defaults if needed.</summary>
        internal static IObservabilityRuntime EnsureInitialized()
        {
            return ObservabilityHost.TryGet() ?? Initialize();
        }
    }
}
