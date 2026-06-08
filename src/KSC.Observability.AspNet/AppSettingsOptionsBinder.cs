using System;
using System.Collections.Specialized;
using System.Configuration;

namespace KSC.Observability.AspNet
{
    /// <summary>
    /// Binds <see cref="ObservabilityOptions"/> from <c>&lt;appSettings&gt;</c> in web.config so an
    /// application can be configured without writing any code. Keys use the
    /// <c>KSC.Observability:</c> prefix, e.g. <c>KSC.Observability:ServiceName</c>.
    /// </summary>
    public static class AppSettingsOptionsBinder
    {
        public const string Prefix = "KSC.Observability:";

        public static ObservabilityOptions Bind(ObservabilityOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            NameValueCollection app;
            try { app = ConfigurationManager.AppSettings; }
            catch { return options; }

            SetString(app, "ServiceName", v => options.ServiceName = v);
            SetString(app, "InstanceId", v => options.InstanceId = v);
            SetString(app, "Environment", v => options.Environment = v);
            SetString(app, "MetricPrefix", v => options.MetricPrefix = v);
            SetString(app, "MetricsPath", v => options.MetricsPath = v);
            SetString(app, "MetricsAccessToken", v => options.MetricsAccessToken = v);

            SetBool(app, "EnableSystemMetrics", v => options.EnableSystemMetrics = v);
            SetBool(app, "EnableHttpMetrics", v => options.EnableHttpMetrics = v);
            SetBool(app, "EnableActiveUserTracking", v => options.EnableActiveUserTracking = v);
            SetBool(app, "TrackRequestPath", v => options.TrackRequestPath = v);

            SetSeconds(app, "SystemMetricsIntervalSeconds", v => options.SystemMetricsInterval = v);
            SetSeconds(app, "ActiveUserWindowSeconds", v => options.ActiveUserWindow = v);

            return options;
        }

        private static void SetString(NameValueCollection app, string key, Action<string> set)
        {
            var raw = app[Prefix + key];
            if (!string.IsNullOrWhiteSpace(raw)) set(raw.Trim());
        }

        private static void SetBool(NameValueCollection app, string key, Action<bool> set)
        {
            var raw = app[Prefix + key];
            if (!string.IsNullOrWhiteSpace(raw) && bool.TryParse(raw.Trim(), out var parsed)) set(parsed);
        }

        private static void SetSeconds(NameValueCollection app, string key, Action<TimeSpan> set)
        {
            var raw = app[Prefix + key];
            if (!string.IsNullOrWhiteSpace(raw) && double.TryParse(raw.Trim(), out var seconds) && seconds > 0)
                set(TimeSpan.FromSeconds(seconds));
        }
    }
}
