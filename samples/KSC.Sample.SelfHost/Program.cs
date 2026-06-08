using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KSC.Observability;
using KSC.Observability.Metrics;

namespace KSC.Sample.SelfHost
{
    /// <summary>
    /// Minimal self-hosted demo of KSC.Observability. Exposes the Prometheus exposition on
    /// http://localhost:9184/metrics and continuously generates synthetic requests and users so
    /// the metrics are non-trivial — handy for wiring up Prometheus/Grafana without IIS.
    /// </summary>
    internal static class Program
    {
        private static readonly Random Rng = new Random();
        private static readonly string[] Paths =
            { "/", "/login", "/api/orders", "/api/products", "/checkout", "/reports/daily" };

        private static void Main(string[] args)
        {
            var prefix = args.Length > 0 ? args[0] : "http://localhost:9184/";

            var runtime = ObservabilityBootstrapper.Initialize(new ObservabilityOptions
            {
                ServiceName = "selfhost-demo",
                Environment = "demo",
                TrackRequestPath = true,
                SystemMetricsInterval = TimeSpan.FromSeconds(2),
                ActiveUserWindow = TimeSpan.FromSeconds(60)
            });

            using var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();

            Console.WriteLine("KSC.Observability self-host demo");
            Console.WriteLine("  listening on : " + prefix);
            Console.WriteLine("  metrics at   : " + prefix + "metrics");
            Console.WriteLine("  generating synthetic load... (Ctrl+C to stop)");

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

            var load = Task.Run(() => GenerateLoad(runtime, cts.Token));
            var serve = Task.Run(() => ServeAsync(listener, runtime, cts.Token));

            cts.Token.WaitHandle.WaitOne();

            try { listener.Stop(); } catch { /* shutting down */ }
            Task.WaitAll(new[] { load, serve }, TimeSpan.FromSeconds(2));
            runtime.Dispose();
            Console.WriteLine("stopped.");
        }

        private static async Task ServeAsync(HttpListener listener, IObservabilityRuntime runtime, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try { ctx = await listener.GetContextAsync().ConfigureAwait(false); }
                catch { break; }
                _ = Task.Run(() => Handle(ctx, runtime));
            }
        }

        private static void Handle(HttpListenerContext ctx, IObservabilityRuntime runtime)
        {
            var path = ctx.Request.Url?.AbsolutePath ?? "/";

            if (string.Equals(path, "/metrics", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
                try { runtime.WriteMetrics(ctx.Response.OutputStream); }
                catch { ctx.Response.StatusCode = 500; }
                ctx.Response.Close();
                return;
            }

            // Treat any other path as a real application request.
            runtime.Http.RequestStarted();
            var sw = Stopwatch.StartNew();
            Thread.Sleep(Rng.Next(5, 120));
            var ip = ctx.Request.RemoteEndPoint?.Address?.ToString() ?? "unknown";
            runtime.Users.Touch("ip:" + ip);
            sw.Stop();

            var code = Rng.Next(100) < 4 ? 500 : 200;
            ctx.Response.StatusCode = code;
            var body = Encoding.UTF8.GetBytes("ok");
            ctx.Response.OutputStream.Write(body, 0, body.Length);
            runtime.Http.RequestCompleted(ctx.Request.HttpMethod ?? "GET", path, code, sw.Elapsed.TotalSeconds);
            ctx.Response.Close();
        }

        private static void GenerateLoad(IObservabilityRuntime runtime, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                runtime.Http.RequestStarted();
                var sw = Stopwatch.StartNew();
                Thread.Sleep(Rng.Next(3, 90));
                runtime.Users.Touch("user-" + Rng.Next(1, 25)); // ~24 distinct concurrent users
                sw.Stop();

                var method = Rng.Next(100) < 70 ? "GET" : "POST";
                var path = Paths[Rng.Next(Paths.Length)];
                var code = Rng.Next(100) < 5 ? 500 : 200;
                runtime.Http.RequestCompleted(method, path, code, sw.Elapsed.TotalSeconds);
            }
        }
    }
}
