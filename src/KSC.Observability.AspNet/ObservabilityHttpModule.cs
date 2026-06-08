using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Web;

namespace KSC.Observability.AspNet
{
    /// <summary>
    /// The ASP.NET integration point. Registered automatically at application start (see
    /// <see cref="OnPreApplicationStart"/>), it:
    /// <list type="bullet">
    /// <item>increments the in-flight gauge and times every request,</item>
    /// <item>records the result (method, status, latency) when the request ends,</item>
    /// <item>tracks distinct active users from the session/identity/ip, and</item>
    /// <item>serves the Prometheus scrape endpoint at <c>ObservabilityOptions.MetricsPath</c>.</item>
    /// </list>
    /// </summary>
    public sealed class ObservabilityHttpModule : IHttpModule
    {
        // Prometheus text exposition format (version 0.0.4).
        private const string ExpositionContentType = "text/plain; version=0.0.4; charset=utf-8";

        private const string StopwatchKey = "__ksc_obs_sw";
        private const string MetricsRequestKey = "__ksc_obs_metrics";

        /// <summary>
        /// Invoked by ASP.NET before <c>Application_Start</c>. Registers this module dynamically so
        /// no web.config edits are required after installing the NuGet package.
        /// </summary>
        public static void OnPreApplicationStart()
        {
            Microsoft.Web.Infrastructure.DynamicModuleHelper.DynamicModuleUtility
                .RegisterModule(typeof(ObservabilityHttpModule));
        }

        public void Init(HttpApplication context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            // Ensure a runtime exists as early as possible (idempotent across HttpApplication instances).
            KscObservability.EnsureInitialized();

            context.BeginRequest += OnBeginRequest;
            context.PostAcquireRequestState += OnPostAcquireRequestState;
            context.EndRequest += OnEndRequest;
        }

        private void OnBeginRequest(object sender, EventArgs e)
        {
            var app = (HttpApplication)sender;
            var context = app.Context;
            var runtime = ObservabilityHost.TryGet();
            if (runtime == null) return;

            if (IsMetricsRequest(context, runtime.Options.MetricsPath))
            {
                context.Items[MetricsRequestKey] = true;
                ServeMetrics(app, context, runtime);
                return;
            }

            if (runtime.Options.EnableHttpMetrics)
            {
                runtime.Http.RequestStarted();
                context.Items[StopwatchKey] = Stopwatch.StartNew();
            }
        }

        private void OnPostAcquireRequestState(object sender, EventArgs e)
        {
            var app = (HttpApplication)sender;
            var context = app.Context;
            if (context.Items[MetricsRequestKey] != null) return;

            var runtime = ObservabilityHost.TryGet();
            if (runtime == null || !runtime.Options.EnableActiveUserTracking) return;

            var key = ResolveUserKey(context);
            if (key != null) runtime.Users.Touch(key);
        }

        private void OnEndRequest(object sender, EventArgs e)
        {
            var app = (HttpApplication)sender;
            var context = app.Context;
            if (context.Items[MetricsRequestKey] != null) return;

            var runtime = ObservabilityHost.TryGet();
            if (runtime == null || !runtime.Options.EnableHttpMetrics) return;

            if (!(context.Items[StopwatchKey] is Stopwatch sw)) return;
            sw.Stop();

            var method = context.Request.HttpMethod;
            var status = context.Response.StatusCode;
            string? path = runtime.Options.TrackRequestPath ? NormalizePath(context) : null;

            runtime.Http.RequestCompleted(method, path, status, sw.Elapsed.TotalSeconds);
        }

        private static bool IsMetricsRequest(HttpContext context, string metricsPath)
        {
            var path = context.Request.Path;
            if (string.IsNullOrEmpty(path)) return false;
            return path.Equals(metricsPath, StringComparison.OrdinalIgnoreCase)
                   || path.EndsWith(metricsPath, StringComparison.OrdinalIgnoreCase);
        }

        private static void ServeMetrics(HttpApplication app, HttpContext context, IObservabilityRuntime runtime)
        {
            var response = context.Response;

            var token = runtime.Options.MetricsAccessToken;
            if (!string.IsNullOrEmpty(token))
            {
                var auth = context.Request.Headers["Authorization"];
                if (!string.Equals(auth, "Bearer " + token, StringComparison.Ordinal))
                {
                    response.StatusCode = 401;
                    response.ContentType = "text/plain";
                    response.Write("Unauthorized");
                    app.CompleteRequest();
                    return;
                }
            }

            response.Clear();
            response.ContentType = ExpositionContentType;
            response.StatusCode = 200;
            try
            {
                runtime.WriteMetrics(response.OutputStream);
            }
            catch (Exception ex)
            {
                response.StatusCode = 500;
                response.ContentType = "text/plain";
                response.Write("Failed to collect metrics: " + ex.Message);
            }
            app.CompleteRequest();
        }

        private static string? ResolveUserKey(HttpContext context)
        {
            try
            {
                var session = context.Session;
                if (session != null && !string.IsNullOrEmpty(session.SessionID))
                    return "sid:" + session.SessionID;
            }
            catch
            {
                // Session state may be unavailable for this handler; fall through.
            }

            IIdentity? identity = context.User?.Identity;
            if (identity != null && identity.IsAuthenticated && !string.IsNullOrEmpty(identity.Name))
                return "usr:" + identity.Name;

            var ip = context.Request.UserHostAddress;
            return string.IsNullOrEmpty(ip) ? null : "ip:" + ip;
        }

        private static string NormalizePath(HttpContext context)
        {
            var path = context.Request.AppRelativeCurrentExecutionFilePath;
            if (string.IsNullOrEmpty(path)) path = context.Request.Path;
            return string.IsNullOrEmpty(path) ? "unknown" : path!;
        }

        public void Dispose()
        {
            // The runtime is process-wide and outlives individual HttpApplication instances.
        }
    }
}
