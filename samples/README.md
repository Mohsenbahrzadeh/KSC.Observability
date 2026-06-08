# Sample: KSC.Sample.WebApp

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
