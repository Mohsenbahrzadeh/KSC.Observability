# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - 2026-06-08

### Added
- Core abstractions (`KSC.Observability.Abstractions`): `ObservabilityOptions`,
  `IObservabilityRuntime`, `IHttpMetricsRecorder`, `IActiveUserTracker`,
  `ISystemMetricsCollector`, metric/label names.
- Prometheus metric backend (`KSC.Observability.Metrics`): system, HTTP and active-user
  collectors, isolated registry with `service`/`instance`/`env` static labels, text exposition.
- ASP.NET integration (`KSC.Observability.AspNet`): self-registering `HttpModule`, `/metrics`
  endpoint, active-user tracking, optional bearer-token protection, `web.config` and code config.
- NuGet packaging with symbols, embedded README and Source Link.
- Prometheus + Grafana stack and a provisioned overview dashboard under `deploy/`.
- Sample ASP.NET Web Forms app under `samples/`.
- GitHub Actions CI (build, test, pack) on Windows.

[Unreleased]: https://github.com/KSC/KSC.Observability/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/KSC/KSC.Observability/releases/tag/v0.1.0
