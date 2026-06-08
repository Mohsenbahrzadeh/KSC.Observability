# KSC.Observability

Drop-in metrics & monitoring for **.NET Framework** applications (ASP.NET Web Forms / MVC),
built on [Prometheus](https://prometheus.io/) and visualized with
[Grafana](https://grafana.com/).

Install one NuGet package into your app and you immediately get a `/metrics` endpoint exposing:

- 👥 **Active users** — how many distinct users are using the app concurrently
- 🔄 **In-flight requests** — how many requests are being processed right now
- ⏱️ **Request rate & latency** — throughput and response-time histogram per method/status
- 🧠 **CPU & memory** — process CPU %, working set, private & managed memory
- ♻️ **Runtime health** — GC collections, thread/handle counts, uptime

Prometheus scrapes that endpoint; Grafana turns it into dashboards and alerts.

```
┌────────────────────┐    scrape /metrics    ┌────────────┐     ┌──────────┐
│  Your ASP.NET app  │ ◀──────────────────── │ Prometheus │ ──▶ │ Grafana  │
│  + KSC.Observability│                       └────────────┘     └──────────┘
└────────────────────┘
```

## Solution layout

| Project | Layer | Target | Purpose |
|---------|-------|--------|---------|
| `KSC.Observability.Abstractions` | Core | netstandard2.0 | Contracts & options, no dependencies |
| `KSC.Observability.Metrics` | Infrastructure | net472 | Prometheus-based collectors |
| `KSC.Observability.AspNet` | Integration | net472 | `HttpModule`, `/metrics` endpoint, auto-registration |
| `KSC.Sample.WebApp` | Sample | net472 | Reference ASP.NET MVC app |
| `KSC.Observability.Tests` | Tests | net472 | Unit tests |

## Status

🚧 Work in progress — see commit history for staged delivery. Full usage and deployment
instructions land with the documentation stage.

## License

MIT — see [LICENSE](LICENSE).
