# Startup performance and telemetry

Detail for the [aot-impact-analysis](SKILL.md) skill. Covers measuring startup
with [scripts/Measure-AotStartup.ps1](scripts/Measure-AotStartup.ps1) and
capturing OpenTelemetry in the Aspire Dashboard with
[scripts/Start-AspireDashboard.ps1](scripts/Start-AspireDashboard.ps1). For the
full env-var contract and `aspire otel` query language, see
[references/perf-and-otel.md](references/perf-and-otel.md).

## Prerequisite: a full SDK layout

Startup is measured through the **real redist `dotnet.exe` muxer**, which
dispatches to the NativeAOT entrypoint when `DOTNET_CLI_ENABLEAOT=true` and to the
managed CLI otherwise. The muxer and the AOT binary only exist after a full
`build.cmd` / `build.sh` (a bare `dotnet-aot` publish is **not** runnable through
the muxer). If the layout is missing, the script stops with the path it expected
under `artifacts/bin/redist/<Config>/dotnet`.

## Start the dashboard

It blocks, so run it in its own terminal or a detached/background shell:

```powershell
pwsh .github/skills/aot-impact-analysis/scripts/Start-AspireDashboard.ps1
```

Endpoints: UI `http://localhost:18888`, OTLP/gRPC `http://localhost:4317`,
OTLP/HTTP `http://localhost:4318`. It prefers the `aspire` CLI and falls back to
the `mcr.microsoft.com/dotnet/aspire-dashboard` container. With the CLI it prints
a `…/login?t=<token>` URL — pass that whole URL to `aspire otel --dashboard-url`.

## Measure

```powershell
pwsh .github/skills/aot-impact-analysis/scripts/Measure-AotStartup.ps1 -Arguments '--version'
```

- **Scenarios.** By default it compares the *same* muxer with
  `DOTNET_CLI_ENABLEAOT=false` (managed) vs `true` (AOT). Pass
  `-BaselineDotnet <path>` to compare two different builds instead (both AOT-on),
  e.g. a baseline-branch redist vs this branch's.
- **Timing phase (headline).** Runs the command `-Iterations` times (default 30,
  `-Warmup` 5) with the exporter **off**, recording wall-clock; reports
  min/median/mean/p95.
- **Telemetry phase (breakdown).** If a dashboard is reachable at `-OtlpEndpoint`
  (default `http://localhost:4317`), does a few exporter-on runs tagged
  `OTEL_RESOURCE_ATTRIBUTES="scenario=<label>"` so the `dotnet-cli` spans land in
  the dashboard under a distinguishable scenario.
- Writes the report to `artifacts/aot-startup.md`.

Pick a representative `-Arguments`. `--version` is the cleanest pure-startup
probe; a real subcommand (e.g. `--help`, or the first-run path on a clean
`DOTNET_CLI_HOME`) exercises more of the entrypoint.

## Pull the span breakdown

```powershell
aspire otel traces --dashboard-url http://localhost:18888 --format Table --search scenario:aot
aspire otel traces --dashboard-url http://localhost:18888 --format Json  --search scenario:aot -n 5
```

`--search` takes free text plus `field:value` qualifiers (`resource:`, `name:`,
`trace-id:`, `status:`, `duration:>N`) and `@attr:value` for the `scenario` tag.

## Read it

- **Prefer median and P95** for the headline; mean is skewed by occasional
  GC/JIT/disk-cache outliers. The AOT path should show lower startup, most
  visibly on the first-run path.
- Use the span tree only to attribute *where* time went (runtime start vs parse
  vs first-run work) between the `scenario=managed` and `scenario=aot` traces —
  exporter-on runs carry flush overhead, so never use them for the headline.
- Both scenarios report service name `dotnet-cli`; tell them apart by the
  `scenario` resource attribute, not the service name.
