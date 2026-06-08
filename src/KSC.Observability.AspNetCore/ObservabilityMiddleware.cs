using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace KSC.Observability.AspNetCore
{
    /// <summary>
    /// ASP.NET Core middleware that times each request, records the in-flight gauge and per-request
    /// metrics, tracks active users (identity / session / connection ip) and serves the Prometheus
    /// scrape endpoint at <see cref="ObservabilityOptions.MetricsPath"/>.
    /// </summary>
    public sealed class ObservabilityMiddleware
    {
        private const string ExpositionContentType = "text/plain; version=0.0.4; charset=utf-8";

        private readonly RequestDelegate _next;
        private readonly IObservabilityRuntime _runtime;

        public ObservabilityMiddleware(RequestDelegate next, IObservabilityRuntime runtime)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public async Task Invoke(HttpContext context)
        {
            var options = _runtime.Options;
            var path = context.Request.Path.Value ?? "/";

            if (string.Equals(path, options.MetricsPath, StringComparison.OrdinalIgnoreCase))
            {
                await ServeMetricsAsync(context, options).ConfigureAwait(false);
                return;
            }

            if (options.EnableActiveUserTracking)
            {
                TouchUser(context);
            }

            if (!options.EnableHttpMetrics)
            {
                await _next(context).ConfigureAwait(false);
                return;
            }

            _runtime.Http.RequestStarted();
            var startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                await _next(context).ConfigureAwait(false);
            }
            finally
            {
                var elapsed = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
                string? routePath = options.TrackRequestPath ? ResolveRoute(context) : null;
                _runtime.Http.RequestCompleted(
                    context.Request.Method, routePath, context.Response.StatusCode, elapsed);
            }
        }

        private async Task ServeMetricsAsync(HttpContext context, ObservabilityOptions options)
        {
            var token = options.MetricsAccessToken;
            if (!string.IsNullOrEmpty(token))
            {
                var auth = context.Request.Headers.Authorization.ToString();
                if (!string.Equals(auth, "Bearer " + token, StringComparison.Ordinal))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Unauthorized").ConfigureAwait(false);
                    return;
                }
            }

            context.Response.ContentType = ExpositionContentType;
            using var buffer = new MemoryStream();
            _runtime.WriteMetrics(buffer);
            buffer.Position = 0;
            await buffer.CopyToAsync(context.Response.Body).ConfigureAwait(false);
        }

        private void TouchUser(HttpContext context)
        {
            string? key = null;

            var name = context.User?.Identity?.Name;
            if (!string.IsNullOrEmpty(name))
            {
                key = "usr:" + name;
            }
            else
            {
                key = TryGetSessionKey(context);
                if (key == null)
                {
                    var ip = context.Connection?.RemoteIpAddress?.ToString();
                    if (!string.IsNullOrEmpty(ip)) key = "ip:" + ip;
                }
            }

            if (key != null) _runtime.Users.Touch(key);
        }

        private static string? TryGetSessionKey(HttpContext context)
        {
            // Session is optional in ASP.NET Core; touching it without AddSession() throws.
            try
            {
                var session = context.Session;
                if (session != null && session.IsAvailable && !string.IsNullOrEmpty(session.Id))
                    return "sid:" + session.Id;
            }
            catch
            {
                // Session middleware not configured — ignore.
            }
            return null;
        }

        private static string ResolveRoute(HttpContext context)
        {
            if (context.GetEndpoint() is RouteEndpoint endpoint)
            {
                var template = endpoint.RoutePattern.RawText;
                if (!string.IsNullOrEmpty(template)) return "/" + template!.TrimStart('/');
            }
            return context.Request.Path.Value ?? "unknown";
        }
    }
}
