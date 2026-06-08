using System.Web;
using KSC.Observability.AspNet;

// Registers the observability HttpModule before Application_Start, so simply installing the
// NuGet package wires everything up — no web.config <modules> entry required.
[assembly: PreApplicationStartMethod(
    typeof(ObservabilityHttpModule),
    nameof(ObservabilityHttpModule.OnPreApplicationStart))]
