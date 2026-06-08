# Monitoring stack (Prometheus + Grafana)

This folder spins up the backend that stores and visualizes the metrics your apps expose.

## Quick start

```bash
cd deploy
docker compose up -d
```

| Service | URL | Credentials |
|---------|-----|-------------|
| Grafana | http://localhost:3000 | `admin` / `admin` (change on first login) |
| Prometheus | http://localhost:9090 | — |

Grafana opens on the **KSC.Observability — Overview** dashboard (folder *KSC.Observability*),
already wired to the Prometheus datasource. Use the **Service** dropdown to filter by app.

## Point Prometheus at your apps

Edit [`prometheus/prometheus.yml`](prometheus/prometheus.yml) and list each app's
`/metrics` endpoint under `ksc-dotnet-apps` → `targets`:

```yaml
static_configs:
  - targets:
      - app-server-01:80
      - app-server-02:80
    labels:
      team: billing
```

- Apps running on the **same machine** as Docker Desktop are reachable at
  `host.docker.internal:<port>`.
- After editing, reload Prometheus without downtime:
  `curl -X POST http://localhost:9090/-/reload`
- Confirm targets are healthy at http://localhost:9090/targets.

### Protected endpoints

If you set `ObservabilityOptions.MetricsAccessToken`, add the matching credentials to the
scrape job:

```yaml
authorization:
  type: Bearer
  credentials: "your-secret-token"
```

## What you get

- **30 days** of metric retention (tune `--storage.tsdb.retention.time`).
- Persistent storage via the `prometheus-data` and `grafana-data` volumes.
- Dashboards and datasource are provisioned from files, so the stack is reproducible.

## Tear down

```bash
docker compose down            # keep data
docker compose down -v         # also delete stored metrics/dashboards
```
