using System;
using KSC.Observability.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KSC.Observability.AspNetCore
{
    /// <summary>
    /// Registration helpers for ASP.NET Core (.NET 8+) applications.
    /// </summary>
    public static class KscObservabilityServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the observability runtime as a singleton. Options are bound from the
        /// <c>KSC.Observability</c> configuration section (appsettings.json / environment), then
        /// the optional <paramref name="configure"/> callback can override them in code.
        /// </summary>
        public static IServiceCollection AddKscObservability(
            this IServiceCollection services,
            Action<ObservabilityOptions>? configure = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            services.AddSingleton<IObservabilityRuntime>(sp =>
            {
                var options = new ObservabilityOptions();
                var configuration = sp.GetService<IConfiguration>();
                configuration?.GetSection("KSC.Observability").Bind(options);
                configure?.Invoke(options);
                return ObservabilityBootstrapper.Initialize(options);
            });

            return services;
        }
    }
}
