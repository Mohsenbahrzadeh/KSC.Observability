using System;
using System.Threading;

namespace KSC.Observability
{
    /// <summary>
    /// Process-wide accessor for the active <see cref="IObservabilityRuntime"/>. The metrics
    /// backend assigns the runtime during bootstrap; integration layers read it per request.
    /// </summary>
    public static class ObservabilityHost
    {
        private static readonly object Gate = new object();
        private static IObservabilityRuntime? _current;

        /// <summary>Whether a runtime has been installed.</summary>
        public static bool IsInitialized => Volatile.Read(ref _current) != null;

        /// <summary>The active runtime, or throws if none has been installed yet.</summary>
        public static IObservabilityRuntime Current =>
            Volatile.Read(ref _current)
            ?? throw new InvalidOperationException(
                "KSC.Observability has not been initialized. Ensure the integration package is installed and bootstrapped.");

        /// <summary>Returns the active runtime, or <c>null</c> if none has been installed.</summary>
        public static IObservabilityRuntime? TryGet() => Volatile.Read(ref _current);

        /// <summary>
        /// Installs <paramref name="runtime"/> as the process-wide runtime. The first writer wins;
        /// a later runtime is disposed to avoid leaking background timers.
        /// </summary>
        public static void SetRuntime(IObservabilityRuntime runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            lock (Gate)
            {
                if (_current != null)
                {
                    runtime.Dispose();
                    return;
                }
                Volatile.Write(ref _current, runtime);
            }
        }
    }
}
