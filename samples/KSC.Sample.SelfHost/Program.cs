using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KSC.Observability;
using KSC.Observability.Metrics;

namespace KSC.Sample.SelfHost
{
    /// <summary>
    /// Minimal self-hosted demo of KSC.Observability. Serves a tiny HTTP endpoint over a raw
    /// <see cref="TcpListener"/> bound to all interfaces (so a Prometheus running in Docker can
    /// scrape it via host.docker.internal, without needing Administrator/urlacl), and continuously
    /// generates synthetic requests and users so the metrics are non-trivial.
    ///
    ///   GET /         -> a small landing page
    ///   GET /metrics  -> the Prometheus exposition
    ///   GET /anything -> counted as a simulated application request
    /// </summary>
    internal static class Program
    {
        private static readonly Random Rng = new Random();
        private static readonly string[] Paths =
            { "/", "/login", "/api/orders", "/api/products", "/checkout", "/reports/daily" };

        private static void Main(string[] args)
        {
            int port = 9184;
            if (args.Length > 0 && int.TryParse(args[0], out var p)) port = p;

            var runtime = ObservabilityBootstrapper.Initialize(new ObservabilityOptions
            {
                ServiceName = "selfhost-demo",
                Environment = "demo",
                TrackRequestPath = true,
                SystemMetricsInterval = TimeSpan.FromSeconds(2),
                ActiveUserWindow = TimeSpan.FromSeconds(60)
            });

            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            Console.WriteLine("KSC.Observability self-host demo");
            Console.WriteLine("  listening on : http://0.0.0.0:" + port);
            Console.WriteLine("  landing page : http://localhost:" + port + "/");
            Console.WriteLine("  metrics at   : http://localhost:" + port + "/metrics");
            Console.WriteLine("  generating synthetic load... (Ctrl+C to stop)");

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

            var load = Task.Run(() => GenerateLoad(runtime, cts.Token));
            var serve = Task.Run(() => AcceptLoopAsync(listener, runtime, cts.Token));

            cts.Token.WaitHandle.WaitOne();

            try { listener.Stop(); } catch { /* shutting down */ }
            Task.WaitAll(new[] { load, serve }, TimeSpan.FromSeconds(2));
            runtime.Dispose();
            Console.WriteLine("stopped.");
        }

        private static async Task AcceptLoopAsync(TcpListener listener, IObservabilityRuntime runtime, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync().ConfigureAwait(false); }
                catch { break; }
                _ = Task.Run(() => HandleClient(client, runtime));
            }
        }

        private static void HandleClient(TcpClient client, IObservabilityRuntime runtime)
        {
            try
            {
                client.NoDelay = true;
                using (client)
                using (var stream = client.GetStream())
                {
                    stream.ReadTimeout = 3000;
                    // Drain the whole request (request line + headers). If we close the socket while
                    // unread bytes remain in the receive buffer, Windows sends a RST and the client
                    // sees "unexpected EOF" instead of our response.
                    var requestLine = ReadRequest(stream);

                    string method = "GET", path = "/";
                    var parts = requestLine.Split(' ');
                    if (parts.Length >= 2) { method = parts[0]; path = parts[1]; }
                    var q = path.IndexOf('?');
                    if (q >= 0) path = path.Substring(0, q);

                    if (string.Equals(path, "/metrics", StringComparison.OrdinalIgnoreCase))
                    {
                        byte[] body;
                        using (var ms = new MemoryStream())
                        {
                            runtime.WriteMetrics(ms);
                            body = ms.ToArray();
                        }
                        WriteResponse(stream, 200, "text/plain; version=0.0.4; charset=utf-8", body);
                        return;
                    }

                    if (path == "/" || string.Equals(path, "/index.html", StringComparison.OrdinalIgnoreCase))
                    {
                        WriteResponse(stream, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(LandingHtml));
                        return;
                    }

                    // Any other path is counted as a real application request.
                    runtime.Http.RequestStarted();
                    var sw = Stopwatch.StartNew();
                    Thread.Sleep(Rng.Next(5, 120));
                    var ip = (client.Client.RemoteEndPoint as IPEndPoint)?.Address?.ToString() ?? "unknown";
                    runtime.Users.Touch("ip:" + ip);
                    sw.Stop();

                    var code = Rng.Next(100) < 4 ? 500 : 200;
                    runtime.Http.RequestCompleted(method, path, code, sw.Elapsed.TotalSeconds);
                    WriteResponse(stream, code, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("ok"));
                }
            }
            catch
            {
                // A malformed/aborted connection must not crash the demo.
            }
        }

        /// <summary>
        /// Reads the full request head (request line + headers, up to the blank line) and returns
        /// the first line. Draining the headers is what prevents a RST on close.
        /// </summary>
        private static string ReadRequest(NetworkStream stream)
        {
            var sb = new StringBuilder(512);
            string? firstLine = null;
            int b;
            try
            {
                while ((b = stream.ReadByte()) != -1)
                {
                    sb.Append((char)b);
                    if (b == '\n')
                    {
                        if (firstLine == null)
                            firstLine = sb.ToString().TrimEnd('\r', '\n');

                        // End of header block: "...\r\n\r\n" (or lenient "\n\n").
                        int n = sb.Length;
                        bool blankLine =
                            (n >= 4 && sb[n - 1] == '\n' && sb[n - 2] == '\r' && sb[n - 3] == '\n' && sb[n - 4] == '\r') ||
                            (n >= 2 && sb[n - 1] == '\n' && sb[n - 2] == '\n');
                        if (blankLine) break;
                    }
                    if (sb.Length > 16384) break; // guard against oversized/abusive headers
                }
            }
            catch { /* read timeout / closed */ }
            return firstLine ?? sb.ToString().TrimEnd('\r', '\n');
        }

        private static void WriteResponse(NetworkStream stream, int statusCode, string contentType, byte[] body)
        {
            var reason = statusCode == 200 ? "OK" : statusCode == 500 ? "Internal Server Error" : "Status";
            var header = "HTTP/1.1 " + statusCode + " " + reason + "\r\n"
                       + "Content-Type: " + contentType + "\r\n"
                       + "Content-Length: " + body.Length + "\r\n"
                       + "Connection: close\r\n"
                       + "\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
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

        private const string LandingHtml =
            "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
            "<title>KSC.Observability demo</title>" +
            "<style>body{font-family:Segoe UI,sans-serif;max-width:720px;margin:3rem auto;line-height:1.6}" +
            "code{background:#f2f2f2;padding:.1rem .3rem;border-radius:3px}a{color:#2563eb}</style></head><body>" +
            "<h1>KSC.Observability — self-host demo</h1>" +
            "<p>This process exposes live Prometheus metrics and generates synthetic traffic.</p>" +
            "<ul>" +
            "<li><a href=\"/metrics\">/metrics</a> — the Prometheus exposition</li>" +
            "<li>Prometheus: <a href=\"http://localhost:9090\">http://localhost:9090</a></li>" +
            "<li>Grafana: <a href=\"http://localhost:3000\">http://localhost:3000</a> (admin/admin)</li>" +
            "</ul></body></html>";
    }
}
