using System;
using System.IO;
using System.Linq;
using System.Text;
using KSC.Observability;
using KSC.Observability.Metrics;
using Xunit;

namespace KSC.Observability.Tests
{
    public class ObservabilityRuntimeTests
    {
        private static string Collect(IObservabilityRuntime runtime)
        {
            using var ms = new MemoryStream();
            runtime.WriteMetrics(ms);
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        /// <summary>Returns the trailing sample value of the first (non-comment) line for a metric.</summary>
        private static string SampleValue(string text, string metricName)
        {
            var line = text.Split('\n')
                .Select(l => l.Trim())
                .FirstOrDefault(l =>
                    !l.StartsWith("#") &&
                    (l.StartsWith(metricName + "{") || l.StartsWith(metricName + " ")));
            if (string.IsNullOrEmpty(line)) return string.Empty;
            return line!.Substring(line.LastIndexOf(' ') + 1);
        }

        private static PrometheusObservabilityRuntime NewRuntime()
        {
            // System metrics off: avoids background timers and keeps the exposition deterministic.
            return new PrometheusObservabilityRuntime(new ObservabilityOptions
            {
                ServiceName = "test-svc",
                InstanceId = "test-instance",
                Environment = "test",
                EnableSystemMetrics = false
            });
        }

        [Fact]
        public void Exposition_IncludesStaticLabels()
        {
            using var runtime = NewRuntime();
            var text = Collect(runtime);

            Assert.Contains("service=\"test-svc\"", text);
            Assert.Contains("instance=\"test-instance\"", text);
            Assert.Contains("env=\"test\"", text);
            Assert.Contains("ksc_build_info", text);
        }

        [Fact]
        public void RequestMetrics_AreRecorded()
        {
            using var runtime = NewRuntime();

            runtime.Http.RequestStarted();
            runtime.Http.RequestCompleted("get", null, 200, 0.42);

            var text = Collect(runtime);

            Assert.Contains("ksc_http_requests_total", text);
            Assert.Contains("method=\"GET\"", text);
            Assert.Contains("code=\"200\"", text);
            Assert.Contains("ksc_http_request_duration_seconds", text);
            // RequestStarted then RequestCompleted -> in-flight back to zero.
            Assert.Equal("0", SampleValue(text, "ksc_http_requests_in_flight"));
        }

        [Fact]
        public void ActiveUsers_AreExposed()
        {
            using var runtime = NewRuntime();

            runtime.Users.Touch("u1");
            runtime.Users.Touch("u2");
            ((PrometheusActiveUserTracker)runtime.Users).Prune();

            var text = Collect(runtime);

            Assert.Equal("2", SampleValue(text, "ksc_active_users"));
        }
    }
}
