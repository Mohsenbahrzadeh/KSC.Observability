using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace KSC.Observability.AspNetCore
{
    /// <summary>
    /// Pipeline helpers for ASP.NET Core (.NET 8+) applications.
    /// </summary>
    public static class KscObservabilityApplicationBuilderExtensions
    {
        private const string ExpositionContentType = "text/plain; version=0.0.4; charset=utf-8";

        /// <summary>
        /// Adds the observability middleware (request metrics, active users) and serves the
        /// Prometheus endpoint at the configured path. A single call is all most apps need:
        /// <code>app.UseKscObservability();</code>
        /// </summary>
        public static IApplicationBuilder UseKscObservability(this IApplicationBuilder app)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));

            // Force the runtime to be created now so the system-metrics sampler starts immediately,
            // and fail fast if AddKscObservability() was not called.
            _ = app.ApplicationServices.GetRequiredService<IObservabilityRuntime>();

            return app.UseMiddleware<ObservabilityMiddleware>();
        }

        /// <summary>
        /// Alternative for apps that prefer endpoint routing. Use this instead of relying on the
        /// middleware to serve the endpoint: <code>app.MapKscMetrics();</code>
        /// </summary>
        public static IEndpointConventionBuilder MapKscMetrics(this IEndpointRouteBuilder endpoints, string? path = null)
        {
            if (endpoints == null) throw new ArgumentNullException(nameof(endpoints));

            var runtime = endpoints.ServiceProvider.GetRequiredService<IObservabilityRuntime>();
            var metricsPath = path ?? runtime.Options.MetricsPath;

            return endpoints.MapGet(metricsPath, async context =>
            {
                context.Response.ContentType = ExpositionContentType;
                using var buffer = new MemoryStream();
                runtime.WriteMetrics(buffer);
                buffer.Position = 0;
                await buffer.CopyToAsync(context.Response.Body).ConfigureAwait(false);
            });
        }
    }
}
