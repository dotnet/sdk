# Perf & OpenTelemetry reference

How the .NET CLI emits OpenTelemetry, how to capture it with the Aspire Dashboard, and how to read
the results for an AOT before/after.

## How the CLI exports telemetry

`src/Cli/dotnet/Telemetry/TelemetryClient.cs` registers an OpenTelemetry tracer + meter under the
service name **`dotnet-cli`** (`ActivitySource` name `dotnet-cli`, defined in
`src/Cli/Microsoft.DotNet.Cli.Utils/Activities.cs`). The OTLP exporter is wired up when **either**:

- `DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER=1` (SDK-specific opt-in), **or**
- any standard OTLP env var is set (`OTEL_EXPORTER_OTLP_ENDPOINT`, `..._PROTOCOL`, `..._HEADERS`, the
  `_TRACES_`/`_METRICS_` variants, etc.) and `OTEL_SDK_DISABLED` is not set.

When enabled the exporter is added with no inline config, so it honors the standard OTLP env vars
(endpoint, protocol, headers, timeout). Source of truth: `EnvironmentVariableNames.OtlpExporterEnvVars`.

Useful knobs:

| Variable | Effect |
| --- | --- |
| `DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER=1` | Turn the OTLP exporter on. |
| `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317` | Where to send OTLP (the dashboard's gRPC receiver). |
| `OTEL_EXPORTER_OTLP_PROTOCOL=grpc` | Use gRPC (port 4317) vs `http/protobuf` (port 4318). |
| `OTEL_RESOURCE_ATTRIBUTES=scenario=aot` | Tag a run so before/after are distinguishable in the dashboard. |
| `DOTNET_CLI_ENABLEAOT=true` | Make the redist muxer dispatch to the **NativeAOT** entrypoint. |
| `DOTNET_CLI_TELEMETRY_OPTOUT=1` | Disable telemetry entirely (used during the clean timing phase). |

The CLI also propagates trace context to child processes via the `TRACEPARENT`/`TRACESTATE` env vars,
so spans from sub-invocations (e.g. an out-of-proc command) chain under the same trace.

## Aspire Dashboard endpoints

`Start-AspireDashboard.ps1` runs a standalone dashboard. Default endpoints:

- UI: `http://localhost:18888`
- OTLP/gRPC: `http://localhost:4317`  -> set `OTEL_EXPORTER_OTLP_ENDPOINT` to this
- OTLP/HTTP: `http://localhost:4318`

With the `aspire` CLI, the dashboard prints a login URL like `http://localhost:18888/login?t=<token>`.
Pass that whole URL to `aspire otel --dashboard-url` and the token is exchanged for an API key.
With the container fallback, anonymous access is enabled so no token is needed.

## Querying telemetry from the terminal (`aspire otel`)

`aspire otel` reads from the dashboard's telemetry API. Subcommands: `traces`, `spans`, `logs`.

```
# Trace summaries for the AOT run
aspire otel traces --dashboard-url http://localhost:18888 --format Table --search scenario:aot

# Full span tree for one trace, as JSON (for scripting / parsing durations)
aspire otel traces --dashboard-url http://localhost:18888 --format Json --trace-id <id>
```

`--search` supports free text and `field:value` qualifiers (`resource:`, `name:`, `trace-id:`,
`status:`, `duration:>N`) and `@attr:value` for custom attributes such as the `scenario` tag.
Metrics are best viewed in the dashboard UI (charts/histograms); traces+spans are the CLI's strength.

## Reading an AOT before/after

- **Wall-clock (primary).** `Measure-AotStartup.ps1` reports min/median/mean/p95 with the exporter
  **off**. Prefer **median** and **P95** over mean for startup (mean is skewed by occasional GC/JIT
  or disk-cache outliers). The AOT path should show lower startup, especially first-run.
- **Span breakdown (secondary).** Compare the `dotnet-cli` root span and notable child spans between
  the `scenario=managed` and `scenario=aot` traces to attribute *where* the time went (runtime
  startup vs. parse vs. first-run work). Export-on runs add small flush overhead, so don't use them
  for the headline numbers - only for the breakdown.
- Run on a warm OS file cache (the script's warmup handles JIT/cache priming) and on a quiet machine.

## Gotchas

- Both managed and AOT runs report service name `dotnet-cli`; rely on the `scenario`/`build`
  resource attribute (not the service name) to tell them apart.
- The redist muxer must be fully laid out (`build.cmd`/`build.sh`) for `DOTNET_CLI_ENABLEAOT` to find
  the AOT binary. A partial `dotnet-aot` publish alone is **not** runnable through the muxer.
- Size and startup are independent: a size regression doesn't imply a startup regression (e.g. extra
  cold code paths that never run at startup), and vice-versa. Report both.
