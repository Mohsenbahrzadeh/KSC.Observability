using System;
using System.Web;
using KSC.Observability;          // ObservabilityOptions
using KSC.Observability.AspNet;   // KscObservability

namespace KSC.Sample.WebApp
{
    public class Global : HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            // OPTIONAL: the HttpModule auto-initializes from Web.config on the first request,
            // so this whole call can be omitted. It is shown here to demonstrate code-based
            // configuration, which overrides any matching Web.config <appSettings> values.
            KscObservability.Initialize(options =>
            {
                options.ServiceName = "sample-web-app";
                options.Environment = "development";
                // options.MetricsAccessToken = "super-secret";   // protect /metrics if desired
                // options.TrackRequestPath = true;               // add a path label (watch cardinality)
            });
        }
    }
}
