# Microsoft.DotNet.Cli.Telemetry

A persist-then-drain [OpenTelemetry](https://opentelemetry.io/) trace and log exporter for **short-lived
command-line processes**, targeting [Azure Monitor / Application Insights](https://learn.microsoft.com/azure/azure-monitor/app/app-insights-overview).

The standard `Azure.Monitor.OpenTelemetry.Exporter` is designed for long-running services that
stay alive long enough to batch and flush telemetry over the network. A CLI process such as
`dotnet` typically exits in well under a second — often before the exporter's background flush
has a chance to run — so its telemetry is unreliably delivered (see
[dotnet/sdk#55184](https://github.com/dotnet/sdk/issues/55184)).

This library solves that by decoupling *capturing* telemetry from *transmitting* it:

1. **Persist (synchronous).** As each span ends — or each log record is emitted — it is serialized
   to the Application Insights "Breeze" wire format and written to durable on-disk storage. This
   completes before the process exits, so telemetry is never lost to an early shutdown.
2. **Drain (background, cross-invocation).** A background uploader opportunistically POSTs
   persisted telemetry — from this run *and* previous runs — to the ingestion endpoint. Because
   a CLI process usually exits before draining its *own* telemetry, delivery is
   eventually-consistent across invocations: the *next* `dotnet` command uploads what the
   previous one persisted.

> **Note:** This is an internal .NET SDK building block (`IsPackable=false`); it is not shipped
> on NuGet. It is consumed by the `dotnet` CLI and the Native-AOT `dotnet-aot` CLI, and is
> therefore trim- and AOT-safe.

## Usage

### Traces

Register the exporter on a `TracerProviderBuilder` exactly like any other OpenTelemetry
exporter (`AddOtlpExporter`, `AddAzureMonitorTraceExporter`, …), using the Options pattern:

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Microsoft.DotNet.Cli")
    .SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService(
            serviceName: "dotnet",
            serviceNamespace: "Microsoft.DotNet.Cli",
            serviceVersion: "9.0.100",
            serviceInstanceId: Environment.MachineName))
    .AddPersistentStorageExporter(options =>
    {
        options.ConnectionString = "InstrumentationKey=<guid>;IngestionEndpoint=https://<region>.in.applicationinsights.azure.com/";
        options.StorageDirectory = Path.Combine(cliTelemetryHome, "PersistedTelemetry");
    })
    .Build();
```

The exporter is registered behind a `SimpleActivityExportProcessor`, so persistence happens
synchronously as each span ends. The background drain is started automatically by the exporter
the first time it exports a span — there is nothing else to start or flush.

### Logs

The same pipeline is available for `ILogger` telemetry through an
`OpenTelemetryLoggerOptions` extension of the *same name*, mirroring how OpenTelemetry's own
`AddOtlpExporter` is overloaded for traces and logs. This is the shape CLIs use when they emit
telemetry as [OpenTelemetry log records](https://opentelemetry.io/docs/specs/otel/logs/) (which
land in the Application Insights **`traces`** table) rather than as spans:

```csharp
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
        logging.SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: "dotnet",
                serviceNamespace: "Microsoft.DotNet.Cli",
                serviceVersion: "9.0.100",
                serviceInstanceId: Environment.MachineName));
        logging.AddPersistentStorageExporter(options =>
        {
            options.ConnectionString = "InstrumentationKey=<guid>;IngestionEndpoint=https://<region>.in.applicationinsights.azure.com/";
            options.StorageDirectory = Path.Combine(cliTelemetryHome, "PersistedTelemetry");
        });
    });
});

ILogger logger = loggerFactory.CreateLogger("Microsoft.DotNet.Cli");
logger.LogInformation("command {command} finished in {durationMs}ms", "build", 1234);
```

The log exporter is registered behind a `SimpleLogRecordExportProcessor`, so — exactly like the
trace exporter — each record is persisted synchronously as it is emitted and the background
drain starts on the first export. Set `IncludeFormattedMessage = true` so the rendered message
is captured, and `IncludeScopes = true` if you want logging scopes stamped onto each record as
properties.

Both signals share one `StorageDirectory`: whichever exporter's drain runs first uploads
*every* persisted blob (traces and logs alike), and leasing prevents any blob from being
uploaded twice. A process that registers both a `TracerProvider` and a logging pipeline against
the same directory therefore drains its combined backlog in a single pass.

## Configuration options

All configuration is supplied through `PersistentStorageTelemetryOptions`:

| Option | Type | Default | Description |
| --- | --- | --- | --- |
| `ConnectionString` | `string?` | `null` | The Application Insights connection string. Its `InstrumentationKey` is stamped into every persisted envelope, and its `IngestionEndpoint` (defaulting to `https://dc.services.visualstudio.com/`) is the upload target. **Required** — if `null`, empty, or unparseable (no instrumentation key), the exporter is **not** registered and the pipeline is disabled. |
| `StorageDirectory` | `string?` | `null` | Directory where telemetry blobs are persisted (Phase 1) and drained from (Phase 2). **Required** — if `null` or empty, the pipeline is disabled. Multiple CLI processes may safely share one directory. |
| `LeasePeriodMilliseconds` | `int` | `30000` | How long a blob is exclusively leased to the draining process while it is being uploaded. If a process exits mid-upload, the lease expires and a later invocation retries the blob. |
| `MaxBlobsPerDrain` | `int` | `200` | Upper bound on blobs uploaded per drain pass, keeping background work bounded when a backlog accumulates. Remaining blobs are drained by later invocations. |

`AddPersistentStorageExporter` is **fail-safe**: when `ConnectionString` or `StorageDirectory`
is missing/invalid it returns the builder (or logger options) unmodified rather than throwing,
so telemetry misconfiguration never breaks the host application.

## How it works

### Phase 1 — persist

`PersistentStorageTraceExporter` maps each `Batch<Activity>` — and `PersistentStorageLogExporter`
each `Batch<LogRecord>` — to newline-delimited JSON (NDJSON) of Application Insights envelopes and
writes it as one durable blob via `OpenTelemetry.PersistentStorage.FileSystem`. Persisted blobs are
stored **uncompressed** so a partially-accepted upload can be re-sliced by envelope index.

### Phase 2 — drain

On its first export the exporter starts a background `PersistentStorageTelemetryUploader` that,
for up to `MaxBlobsPerDrain` blobs:

- **Leases** each blob (an atomic file rename) so concurrent CLI processes never upload the same
  payload, then reads it and POSTs it to `<IngestionEndpoint>/v2.1/track`.
- **Compresses on the wire.** The request body is gzip-compressed (`Content-Encoding: gzip`),
  streamed directly into the request so the compressed bytes are never buffered separately.
- **Handles partial success.** Ingestion may return **HTTP 206 (Partial Content)** with a
  per-item error list. Envelopes rejected with a *retriable* status (`408`, `429`, `439`,
  `500`, `503`) are re-sliced out of the payload and re-persisted for a later attempt; the
  accepted portion is dropped. `200` deletes the blob; other statuses leave it for retry.
- **Never throws.** All failures are swallowed — background telemetry delivery must never affect
  the host CLI. A blob that fails to upload is simply retried after its lease expires.

## Span & resource mapping

The serialized envelopes are byte-compatible with what the Azure Monitor exporter would produce
for the same spans, so existing Application Insights dashboards keep working.

| OpenTelemetry input | Application Insights envelope |
| --- | --- |
| `ActivityKind.Server` / `Consumer` span | `RequestData` |
| `ActivityKind.Internal` span | `RemoteDependencyData` (`type: "InProc"`) |
| `ActivityKind.Client` / `Producer` span | `RemoteDependencyData` |
| `ActivityEvent` | `MessageData` (or `ExceptionData` for OpenTelemetry `exception` events) |

## Log-record mapping

Log records are mapped exactly as `Azure.Monitor.OpenTelemetry.Exporter` maps them (via its
internal `LogsHelper`), so they land in the Application Insights **`traces`** table alongside any
`MessageData` produced from activity events:

| OpenTelemetry `LogRecord` | Application Insights envelope |
| --- | --- |
| A record **without** an exception | `MessageData` |
| A record **with** an `Exception` | `ExceptionData` |

- **Message.** `Exception.Message` wins, then the formatted message, then the raw
  `{OriginalFormat}` template.
- **Severity.** `LogLevel` is projected onto Application Insights `severityLevel`:
  `Trace`/`Debug` → `Verbose`, `Information` → `Information`, `Warning` → `Warning`,
  `Error` → `Error`, `Critical` → `Critical`.
- **Properties.** State attributes (excluding `{OriginalFormat}`) and logging scopes become
  `properties`, along with `CategoryName`, and `EventId` / `EventName` when a non-default
  `EventId` is present.
- **Correlation.** The ambient activity's trace context is stamped as `ai.operation.id`
  (trace id) and `ai.operation.parentId` (span id) so each log row correlates with its span.

Resource attributes are projected onto Application Insights context tags:

| Resource attribute | Context tag |
| --- | --- |
| `service.namespace` + `service.name` | `ai.cloud.role` (joined as `namespace.name`) |
| `service.instance.id` | `ai.cloud.roleInstance` |
| `service.version` | `ai.application.ver` |
| (derived) | `ai.internal.sdkVersion` = `dotnet<runtime>:otel<otel>:ext<exporter>` |

## Design notes

- **Source-generated JSON.** Serialization uses `System.Text.Json` source generation
  (`TelemetryJsonContext`) over declarative POCOs rather than hand-rolled `Utf8JsonWriter`
  code, keeping the wire mapping reviewable and AOT/trim-safe.
- **No hard dependency on the Azure Monitor exporter.** The `ai.internal.sdkVersion` exporter
  version is supplied by the build as assembly metadata (`AzureMonitorOpenTelemetryExporterVersion`),
  so this library does not reference `Azure.Monitor.OpenTelemetry.Exporter` and remains
  source-build friendly.

## Testing against a real ingestion endpoint

Most tests run fully offline against a stubbed `HttpMessageHandler`. There is also an **opt-in**
end-to-end suite (`RealEndpointTelemetryE2ETests`) that POSTs to a *live* Application Insights
ingestion endpoint and asserts the accepted/rejected breakdown the Breeze `/v2.1/track` service
returns. It exists to prove the shipping upload path works against the real service and to act as
a canary if the App Insights ARM ingestion layer makes a breaking wire-contract change.

These tests are **skipped** (reported inconclusive) unless the
`DOTNET_CLI_TELEMETRY_E2E_CONNECTION_STRING` environment variable is set, so they never run — or
touch the network — during a normal CI pass. To run them locally against your own resource:

```powershell
$env:DOTNET_CLI_TELEMETRY_E2E_CONNECTION_STRING = "InstrumentationKey=<guid>;IngestionEndpoint=https://<region>.in.applicationinsights.azure.com/"
dotnet test test/Microsoft.DotNet.Cli.Telemetry.Tests --filter "FullyQualifiedName~RealEndpointTelemetryE2ETests"
```

The Breeze endpoint reports `itemsReceived` / `itemsAccepted` / `errors` **synchronously** in the
POST response, so drop rate is measured immediately — the ~1 hour ingestion delay only applies to
querying the emitted rows back out through the Analytics API.

### Verifying end-to-end ingestion after the tests run

The dotnet CLI does not query the raw Application Insights tables directly — its telemetry flows
through a processing pipeline into a downstream destination table. That pipeline only forwards
events emitted in the **real CLI event shape** (`ActivityEvent`s named `dotnet/cli/<event>`, as
`TelemetryClient` produces); a synthetic event name is dropped and never reaches the table. So the
harness emits a genuine `dotnet/cli/toplevelparser/command` event — the event every CLI invocation
sends — rather than a made-up name.

Specific runs are looked up by the **`SessionId`** common telemetry property, so each run stamps
its id there. By default the id is a fresh GUID (written to the test output), but you can **pin** it
up front via `DOTNET_CLI_TELEMETRY_E2E_RUN_ID` so a script can choose the id, run the tests, wait
for ingestion, then query for exactly that run:

```powershell
$runId = [guid]::NewGuid().ToString("N")
$env:DOTNET_CLI_TELEMETRY_E2E_RUN_ID = $runId
$env:DOTNET_CLI_TELEMETRY_E2E_CONNECTION_STRING = "InstrumentationKey=<guid>;IngestionEndpoint=https://<region>.in.applicationinsights.azure.com/"
dotnet test test/Microsoft.DotNet.Cli.Telemetry.Tests --filter "FullyQualifiedName~RealEndpointTelemetryE2ETests"
```

After roughly an hour, confirm delivery by searching the .NET CLI telemetry destination table for
rows whose **`SessionId`** equals `<runId>`. The `SessionId` is stamped on both the message
(`dotnet/cli/toplevelparser/command`) and its parent request/dependency spans, so all of a run's
rows share the same id.

## References

- [OpenTelemetry .NET](https://github.com/open-telemetry/opentelemetry-dotnet)
- [Application Insights ingestion (Breeze) format](https://github.com/microsoft/ApplicationInsights-dotnet)
- [dotnet/sdk#55184 — the motivating issue](https://github.com/dotnet/sdk/issues/55184)
