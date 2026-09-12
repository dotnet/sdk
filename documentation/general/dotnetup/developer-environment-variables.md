# dotnetup contributor environment variables

This article inventories environment variables used to develop, diagnose, and
test dotnetup. They aren't supported user controls and can change or be removed
without notice. Supported variables are documented in the
[user-facing reference](reference/environment-variables.md).

## Diagnostics and implementation details

| Variable | Purpose |
| --- | --- |
| `DOTNETUP_TELEMETRY_DEBUG` | When set to `1`, adds console exporters for telemetry logs and activities. |
| `DOTNETUP_CLI_GET_PERF_TRACE` | When set to `1` or `true`, enables network export of OpenTelemetry activities for performance investigation. |
| `DOTNETUP_DEV_BUILD` | When set to `1` or `true`, marks telemetry as coming from a development build and includes the commit SHA in the reported version when available. Debug builds are always marked as development builds. |
| `DOTNET_CLI_TELEMETRY_DISABLE_TRACE_EXPORT` | When set to `1` or `true`, disables Azure Monitor and OTLP export while leaving diagnostic console and disk logging available. |
| `DOTNET_CLI_TELEMETRY_LOG_PATH` | Writes collected activities to a local JSON diagnostic log. dotnetup adds `-dotnetup` to the file name so it doesn't conflict with the .NET CLI log. |
| `DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER` | When set to `1` or `true`, adds the OTLP exporter. Standard `OTEL_EXPORTER_OTLP_*` variables configure its endpoint, protocol, headers, and timeout. `OTEL_SDK_DISABLED` can disable the OpenTelemetry SDK. |
| `DOTNET_CLI_TELEMETRY_PROFILE` | Copies the .NET CLI telemetry profile value into dotnetup telemetry so explicitly profiled runs can be correlated. |
| `DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING` | When set to `1`, prevents dotnetup from changing console output to UTF-8. This is a shared CLI implementation compatibility switch. |
| `DOTNETUP_TELEMETRY_FLUSH_TIMEOUT_MS` | Legacy fallback for `DOTNET_CLI_TELEMETRY_SHUTDOWN_TIMEOUT_MS`. Use the [supported variable](reference/environment-variables.md#dotnetup-variables) instead. |

The OTLP exporter follows the
[OpenTelemetry environment-variable specification](https://opentelemetry.io/docs/specs/otel/configuration/sdk-environment-variables/).

## Test-only hooks

| Variable | Purpose |
| --- | --- |
| `DOTNET_CLI_TELEMETRY_E2E_CONNECTION_STRING` | Replaces the production telemetry connection string so end-to-end tests can use a local ingestion endpoint. |
| `DOTNET_TESTHOOK_DOTNETUP_TELEMETRY_SHUTDOWN_BUDGET_PATH` | Writes the selected telemetry shutdown budget to a file for test observation. |
| `DOTNET_TESTHOOK_MANIFEST_PATH` | Replaces the installation manifest path so tests don't modify user state. |
| `DOTNET_TESTHOOK_DEFAULT_DOTNET_PATH` | Replaces the default managed .NET installation path so tests don't modify user installations. |

Tests also commonly set the supported `DOTNET_DOTNETUP_DATA_DIR` and telemetry
variables to isolate state and network activity. Those variables retain the
behavior described in the [user-facing reference](reference/environment-variables.md);
using them in a test doesn't make them test-only.
