using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
#if NET48
using System.Data.SqlClient;
#else
using Microsoft.Data.SqlClient;
#endif

namespace KSC.Observability
{
    /// <summary>probe وابستگی‌ها (SQL / HTTP / TCP) و ثبت نتیجه در متریک‌ها.</summary>
    public static class DependencyChecker
    {
        private static readonly HttpClient Http = new HttpClient();

        public static async Task<DependencyStatus> CheckAsync(KscDependencyOptions dep, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            string? error = null;
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, dep.TimeoutSeconds)));
                try
                {
                    switch (dep.Type)
                    {
                        case KscDependencyType.Sql: await CheckSqlAsync(dep.Target, cts.Token).ConfigureAwait(false); break;
                        case KscDependencyType.Tcp: await CheckTcpAsync(dep.Target, cts.Token).ConfigureAwait(false); break;
                        default: await CheckHttpAsync(dep.Target, cts.Token).ConfigureAwait(false); break;
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { error = "timeout"; }
                catch (Exception ex) { error = Short(ex); }
            }
            sw.Stop();

            var status = new DependencyStatus
            {
                Name = dep.Name,
                Type = dep.Type,
                Critical = dep.Critical,
                Healthy = error == null,
                LatencyMs = sw.Elapsed.TotalMilliseconds,
                Error = error,
                CheckedAtUtc = DateTime.UtcNow,
            };

            var tag = new KeyValuePair<string, object?>("dependency", dep.Name);
            KscTelemetry.DependencyDuration.Record(sw.Elapsed.TotalSeconds, tag);
            if (error != null) KscTelemetry.DependencyFailures.Add(1, tag);
            KscTelemetry.SetStatus(status);
            return status;
        }

        private static async Task CheckSqlAsync(string connectionString, CancellationToken ct)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync(ct).ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT 1";
                    cmd.CommandTimeout = 5;
                    await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                }
            }
        }

        private static async Task CheckHttpAsync(string url, CancellationToken ct)
        {
            using (var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                // 4xx یعنی سرویس بالاست (مثلاً 401)؛ فقط 5xx/عدم پاسخ خرابی است
                if ((int)resp.StatusCode >= 500) throw new HttpRequestException("HTTP " + (int)resp.StatusCode);
            }
        }

        private static async Task CheckTcpAsync(string hostPort, CancellationToken ct)
        {
            var idx = hostPort.LastIndexOf(':');
            if (idx <= 0 || !int.TryParse(hostPort.Substring(idx + 1), out var port))
                throw new ArgumentException("Target must be host:port");
            using (var client = new TcpClient())
            {
                var connect = client.ConnectAsync(hostPort.Substring(0, idx), port);
                var done = await Task.WhenAny(connect, Task.Delay(Timeout.Infinite, ct)).ConfigureAwait(false);
                if (done != connect) throw new OperationCanceledException(ct);
                await connect.ConfigureAwait(false);
            }
        }

        // پیام کوتاه و بدون اطلاعات حساس (connection string/آدرس داخلی در پیام خطا نشت نکند)
        private static string Short(Exception ex)
        {
            var m = ex.GetType().Name;
            if (ex is SqlException se) m += " #" + se.Number;
            return m;
        }
    }
}
