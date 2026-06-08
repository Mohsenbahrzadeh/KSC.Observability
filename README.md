# KSC.Observability

Drop-in metrics & monitoring for **.NET Framework** applications (ASP.NET Web Forms / MVC),
built on [Prometheus](https://prometheus.io/) and visualized with
[Grafana](https://grafana.com/).

Install one NuGet package into your app and you immediately get a `/metrics` endpoint exposing:

- 👥 **Active users** — how many distinct users are using the app concurrently
- 🔄 **In-flight requests** — how many requests are being processed right now
- ⏱️ **Request rate & latency** — throughput and a response-time histogram per method/status
- 🧠 **CPU & memory** — process CPU %, working set, private & managed memory
- ♻️ **Runtime health** — GC collections, thread/handle counts, uptime

Prometheus scrapes that endpoint; Grafana turns it into dashboards and alerts.

> 📘 **راهنمای آموزشی کامل به فارسی:** [`docs/GUIDE.fa.md`](docs/GUIDE.fa.md) — مفاهیم، معماری،
> نصب گام‌به‌گام، تنظیمات، Grafana، PromQL و عیب‌یابی.

```
┌─────────────────────┐   scrape /metrics    ┌────────────┐     ┌──────────┐
│   Your ASP.NET app  │ ◀─────────────────── │ Prometheus │ ──▶ │ Grafana  │
│ + KSC.Observability │                      └────────────┘     └──────────┘
└─────────────────────┘
```

---

## One-command demo

With **Docker running**, from the repo root:

```powershell
.\up.cmd
```

This builds the demo app, starts Prometheus + Grafana, runs a sample app that emits live
metrics, waits until everything is healthy, and opens the Grafana dashboard. Stop it with:

```powershell
.\down.cmd
```

| What | URL |
|------|-----|
| Grafana dashboard | http://localhost:3000/d/ksc-observability-overview (admin / admin) |
| Prometheus targets | http://localhost:9090/targets |
| Demo app / metrics | http://localhost:9184/ · http://localhost:9184/metrics |

> Real environments: run `.\up.cmd -NoDemo` to start only the monitoring stack, then point
> Prometheus at your own IIS apps (see [`deploy/`](deploy/)).

---

## شروع سریع (فارسی)

۱. پکیج را در اپ ASP.NET خود نصب کنید:

```
Install-Package KSC.Observability.AspNet
```

۲. (اختیاری) در `Global.asax` نام سرویس را تنظیم کنید — اگر این کار را نکنید، خودکار از
`web.config` یا مقادیر پیش‌فرض مقداردهی می‌شود:

```csharp
protected void Application_Start(object sender, EventArgs e)
{
    KscObservability.Initialize(o => o.ServiceName = "نام-سیستم-من");
}
```

۳. اپ را اجرا کنید و آدرس `/metrics` را باز کنید؛ همهٔ متریک‌ها آنجا هستند.

۴. استک مانیتورینگ را بالا بیاورید و آن را به اپ‌هایتان وصل کنید:

```bash
cd deploy && docker compose up -d      # Grafana: http://localhost:3000
```

فایل `deploy/prometheus/prometheus.yml` را ویرایش کنید و آدرس `/metrics` هر اپ را اضافه کنید.
داشبورد آماده در Grafana زیر پوشهٔ **KSC.Observability** ظاهر می‌شود.

---

## Installation

```powershell
Install-Package KSC.Observability.AspNet
```

This single package transitively brings `KSC.Observability.Metrics`,
`KSC.Observability.Abstractions`, `prometheus-net` and `Microsoft.Web.Infrastructure`.

> The packages are produced from this repo (`build/pack.ps1`) into `./artifacts`. Push them to
> your internal NuGet feed (Azure Artifacts, BaGet, a file share, …) so your apps can install them.

That's all that's strictly required. The HttpModule **registers itself** via
`[assembly: PreApplicationStartMethod]` — no `web.config` `<modules>` edit needed — and starts:

- timing every request and recording the in-flight gauge,
- tracking active users (from session id / authenticated identity / client ip), and
- serving the Prometheus exposition at `/metrics`.

### Optional code configuration

```csharp
// Global.asax.cs
protected void Application_Start(object sender, EventArgs e)
{
    KscObservability.Initialize(options =>
    {
        options.ServiceName = "billing-portal";
        options.Environment = "production";
        options.TrackRequestPath = false;            // keep label cardinality low
        options.MetricsAccessToken = "super-secret"; // require a bearer token to scrape
    });
}
```

### Optional zero-code configuration (web.config)

```xml
<appSettings>
  <add key="KSC.Observability:ServiceName" value="billing-portal" />
  <add key="KSC.Observability:Environment" value="production" />
  <add key="KSC.Observability:MetricsPath" value="/metrics" />
  <add key="KSC.Observability:ActiveUserWindowSeconds" value="300" />
  <!-- <add key="KSC.Observability:MetricsAccessToken" value="super-secret" /> -->
</appSettings>
```

Code configuration (if present) wins over `web.config`; both fall back to sensible defaults.

---

## Configuration reference

| Option / appSettings key (`KSC.Observability:` prefix) | Default | Meaning |
|--------------------------------------------------------|---------|---------|
| `ServiceName` | `dotnet-app` | `service` label |
| `InstanceId` | machine name | `instance` label |
| `Environment` | `production` | `env` label |
| `MetricPrefix` | `ksc` | prefix on every metric name |
| `MetricsPath` | `/metrics` | scrape endpoint path |
| `EnableSystemMetrics` | `true` | CPU/memory/GC/threads/uptime |
| `EnableHttpMetrics` | `true` | request counter/in-flight/latency |
| `EnableActiveUserTracking` | `true` | active-users gauge |
| `SystemMetricsIntervalSeconds` | `5` | system sampler interval |
| `ActiveUserWindowSeconds` | `300` | sliding window for "active" |
| `TrackRequestPath` | `false` | add a `path` label (cardinality!) |
| `MetricsAccessToken` | _none_ | require `Authorization: Bearer <token>` |

---

## Metrics reference

All metrics carry the `service`, `instance` and `env` labels (configurable prefix shown as `ksc`).

| Metric | Type | Extra labels | Description |
|--------|------|--------------|-------------|
| `ksc_active_users` | gauge | — | Distinct users active within the window |
| `ksc_http_requests_total` | counter | `method`, `code` (`path`*) | Total HTTP requests |
| `ksc_http_requests_in_flight` | gauge | — | Requests being processed right now |
| `ksc_http_request_duration_seconds` | histogram | `method` | Request latency |
| `ksc_process_cpu_usage_percent` | gauge | — | CPU % of one logical core |
| `ksc_process_working_set_bytes` | gauge | — | Physical memory in use |
| `ksc_process_private_memory_bytes` | gauge | — | Private memory |
| `ksc_process_managed_memory_bytes` | gauge | — | Managed GC heap size |
| `ksc_process_threads` | gauge | — | OS thread count |
| `ksc_process_handles` | gauge | — | OS handle count |
| `ksc_process_uptime_seconds` | gauge | — | Seconds since process start |
| `ksc_gc_collections_total` | counter | `generation` | GC collections by generation |
| `ksc_build_info` | gauge | `version` | Always `1`; library version label |

`*` `path` is only present when `TrackRequestPath` is enabled.

### Handy PromQL

```promql
# Concurrent users per app
sum by (service) (ksc_active_users)

# Requests per second
sum by (service) (rate(ksc_http_requests_total[5m]))

# Error ratio (5xx)
sum by (service) (rate(ksc_http_requests_total{code=~"5.."}[5m]))
  / sum by (service) (rate(ksc_http_requests_total[5m]))

# p95 latency
histogram_quantile(0.95, sum by (le) (rate(ksc_http_request_duration_seconds_bucket[5m])))

# Is an instance down? (no scrape)
up{job="ksc-dotnet-apps"} == 0
```

---

## The monitoring stack

See [`deploy/`](deploy/) for a ready-to-run Prometheus + Grafana stack:

```bash
cd deploy
docker compose up -d
```

Then edit `deploy/prometheus/prometheus.yml` to list each app's `/metrics` endpoint and reload
Prometheus. Grafana opens on the provisioned **KSC.Observability — Overview** dashboard.

---

## Solution layout

| Project | Layer | Target | Purpose |
|---------|-------|--------|---------|
| `KSC.Observability.Abstractions` | Core | netstandard2.0 | Contracts & options, no dependencies |
| `KSC.Observability.Metrics` | Infrastructure | net472 | Prometheus-based collectors |
| `KSC.Observability.AspNet` | Integration | net472 | `HttpModule`, `/metrics`, auto-registration |
| `KSC.Sample.WebApp` | Sample | net472 | Reference ASP.NET app (Visual Studio) |
| `KSC.Observability.Tests` | Tests | net472 | Unit tests |

Dependency direction points inward: `AspNet → Metrics → Abstractions`. The integration layer is
the composition root; the inner layers never reference outward, so the metric backend can be
swapped without touching the contracts.

---

## Build from source

```powershell
# Restore, test and pack into ./artifacts
./build/pack.ps1

# Or step by step
dotnet build  KSC.Observability.sln -c Release
dotnet test   KSC.Observability.sln -c Release
dotnet pack   KSC.Observability.sln -c Release
```

Requires the .NET SDK (pinned in `global.json`) and .NET Framework 4.7.2 targeting pack.

---

## License

MIT — see [LICENSE](LICENSE).
