# Samples

Three ways to see KSC.Observability running.

## KSC.Sample.WebApi — ASP.NET Core / .NET 8

A minimal API showing the two-line setup for modern .NET. Run it:

```bash
dotnet run --project samples/KSC.Sample.WebApi
# then open /metrics on the printed URL; hit /work a few times to move the histogram
```

Key bits (`Program.cs`):

```csharp
builder.Services.AddKscObservability(o => o.ServiceName = "sample-web-api");
var app = builder.Build();
app.UseKscObservability();   // request metrics + active users + /metrics
```

Options can also come from `appsettings.json` under the `KSC.Observability` section.

## KSC.Sample.SelfHost — runnable in one command (no IIS)

A console app that hosts `/metrics` on an `HttpListener` and generates synthetic traffic, so the
metrics actually move. Great for trying Prometheus/Grafana without setting up IIS.

```bash
dotnet run --project samples/KSC.Sample.SelfHost
# then open http://localhost:9184/metrics
```

You'll see live values such as:

```
ksc_active_users{service="selfhost-demo",...} 24
ksc_http_requests_in_flight{...} 1
ksc_http_requests_total{method="GET",code="200",path="/api/orders",...} 304
ksc_http_request_duration_seconds_bucket{method="GET",le="0.05",...} 827
ksc_process_working_set_bytes{...} 36814848
ksc_process_uptime_seconds{...} 144.3
```

To let the Docker Prometheus scrape it, run as Administrator with a wildcard binding and point
`deploy/prometheus/prometheus.yml` at `host.docker.internal:9184`:

```powershell
# elevated PowerShell
dotnet run --project samples/KSC.Sample.SelfHost -- "http://+:9184/"
```

---

## KSC.Sample.WebApp — realistic ASP.NET integration

A minimal classic **ASP.NET Web Forms** application (net472) showing how a real app consumes
`KSC.Observability.AspNet`.

> This project requires Visual Studio's web tooling and is intentionally **not** part of
> `KSC.Observability.sln`, so the command-line/CI build stays self-contained.

## Run it

1. Produce the package into the local feed (once):
   ```powershell
   ./build/pack.ps1 -SkipTests
   ```
2. Open `KSC.Sample.WebApp.csproj` in Visual Studio 2019/2022.
3. Restore NuGet packages (the local `artifacts` feed is configured in the root `NuGet.config`).
4. Press F5. The site opens in IIS Express.
5. Browse a few pages, then open **`/metrics`** — you'll see the live exposition.

## What to notice

- **No `web.config` `<modules>` entry** — the HttpModule self-registers via
  `[assembly: PreApplicationStartMethod]`.
- `Global.asax.cs` shows optional code configuration via `KscObservability.Initialize`.
- `Web.config` `<appSettings>` shows the equivalent zero-code configuration.

## How your real apps adopt it

```
Install-Package KSC.Observability.AspNet
```

That's it — install, optionally set `ServiceName`, and point Prometheus (see `/deploy`) at the
app's `/metrics` endpoint.
