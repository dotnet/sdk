# dotnetup Telemetry

dotnetup includes a telemetry feature that collects usage data and sends it to
Microsoft when you use dotnetup commands. The usage data includes exception
information when dotnetup crashes. Telemetry data helps the .NET team understand
how the tools are used so they can be improved. Information on failures helps
the team resolve problems and fix bugs.

## How to Opt Out

The dotnetup telemetry feature is enabled by default. To opt out of the telemetry
feature, set the `DOTNET_CLI_TELEMETRY_OPTOUT` environment variable to `1` or `true`.

To suppress the first-run telemetry notice without disabling telemetry, set the
`DOTNET_NOLOGO` environment variable to `1` or `true`.

## First-Run Notice

dotnetup displays the following message on first run:

    dotnetup collects usage data to help improve your experience. You can opt out
    by setting the DOTNET_CLI_TELEMETRY_OPTOUT environment variable to '1'.
    Learn more: https://aka.ms/dotnetup-telemetry

## Data Points

The telemetry feature doesn't collect personal data, such as usernames or email
addresses. It does not scan your code and does not extract project-level data.
The data is sent securely to Microsoft servers using Azure Monitor
(https://azure.microsoft.com/services/monitor/) technology.

Data collected includes:

- Timestamp of invocation
- Command invoked (e.g., "install", "update", "list")
- dotnetup version and commit SHA
- Operating system and architecture
- Whether running in a CI environment
- Whether running from an LLM agent (e.g., GitHub Copilot, Claude)
- Exit code / success or failure status
- For failures: error type, error category, sanitized error details
  (no file paths), and the full stack trace with exception messages removed

## Crash Exception Telemetry

If dotnetup crashes, it collects the name of the exception and the stack trace
of dotnetup code, following the same approach as the .NET SDK. Exception messages
are not included because they may contain user-provided input.
For more details on crash exception telemetry, see the
[.NET CLI telemetry documentation](https://aka.ms/dotnet-cli-telemetry).

### Related Environment Variables

- **`DOTNET_CLI_TELEMETRY_STORAGE_PATH`**: Overrides Azure's storage base directory. Azure creates a partition beneath it for the instrumentation key, user, process name, and application directory.
- **`DOTNET_CLI_TELEMETRY_SHUTDOWN_TIMEOUT_MS`**: Overrides the CI shutdown budget. Nonnegative integer values, including zero, are honored. `DOTNETUP_TELEMETRY_FLUSH_TIMEOUT_MS` is the legacy fallback; otherwise the default is 20 seconds. This policy is independent of the SDK CLI's default.
- **`DOTNETUP_TELEMETRY_FORCE_LOCAL_DELIVERY`**: Uses local persist-and-detached-drain delivery even when CI is detected. Intended for diagnostics and end-to-end validation.

## Delivery and Shutdown

[DotnetupTelemetry](../Telemetry/DotnetupTelemetry.cs) uses Azure Monitor exporter 1.9.0 or later for serialization, storage, and HTTP delivery. Completion LogRecords are the primary signal consumed by data-x from the Application Insights traces table. Exporting the enclosing spans remains opt-in through `DOTNETUP_CLI_GET_PERF_TRACE`; it is not a replacement for completion logs. Trace-based log filtering is disabled so enabling performance traces cannot suppress completion messages.

Normal exports are network-first. On local shutdown Azure switches queued exports to persistence, with no wait for the newly started background drain. Logger shutdown runs first and the tracer receives the remaining budget:

| Invocation | Foreground Shutdown Budget | Detached Child |
|---|---:|---|
| Successful local command | 200 ms | Yes |
| Successful shell-startup command | 10 ms | No |
| Failed local command | 400 ms | Yes, except shell-startup commands |
| CI | 20 seconds by default, overridable | No |

These existing dotnetup budgets are unchanged. Unlike the previous per-record synchronous exporter, Azure persists a queued batch during shutdown. A short budget, particularly shell startup's 10 ms, can expire before records reach disk. A detached child cannot recover records that were never persisted. Shutdown results and process success are not ingestion acknowledgments.

[The detached process](../Telemetry/DotnetupTelemetryDrainProcess.cs) relaunches the same native executable with `--drain-telemetry`, before command parsing or telemetry event creation. It constructs an Azure log exporter using the same connection string and storage base directory, then remains alive for three minutes without emitting new application records. This preserves the existing three-minute lifetime independently of Azure's shutdown-task tracking. It does not exit early on an empty store or claim that all data was accepted. Exporter disposal follows that wait.

A `.drain.lock` handle permits only one child per storage base directory; concurrent attempts return immediately. On Windows the hidden child is launched without inheriting the foreground command's captured pipes. Other platforms retain the redirected child launch. The parent does not wait for child exit. Shell-startup commands still skip child creation, leaving persisted records to later invocations using the same partition.

Azure schedules its eager drain after 50 ms, retains a two-minute periodic timer, and uses three-minute blob leases. Failed or interrupted uploads can therefore remain pending beyond one child's lifetime. HTTP retry, backoff, storage retention, and lease recovery belong to Azure, not dotnetup. Three minutes provides an opportunity for delivery, not a guarantee.

The SDK CLI and dotnetup can share a storage base directory but normally use different Azure partitions. Moving or renaming the executable can also change its partition. Old blobs from the removed custom exporter are left untouched and are not automatically drained by Azure. The migration does not merge partitions or access Azure's private exporter APIs.

## Delivery Tests

[TelemetryDrainE2ETests](../../../../test/dotnetup.Tests/TelemetryDrainE2ETests.cs) run the actual native executable against loopback ingestion. They verify existing stored-data delivery, normal completion LogRecords with and without optional traces, independent CI delivery, foreground exit and output-pipe closure, failure budgets, and the shell-startup budget. The direct-drain case waits for natural child exit after its three-minute lifetime.

The fixture observes `.blob` and `.lock` files recursively, reads complete HTTP bodies including gzip, and checks completion-message payloads. Existing-data fixtures are seeded using the public persistent-storage provider in the partition initialized by dotnetup, rather than assuming that a 10-ms shell-startup run reliably persisted a record. A test-only process identity marker permits cleanup of the specific detached child without changing its production lifetime.

Console-exporter error/schema tests remain separate and disable Azure networking. Live acceptance checks in the SDK exporter test project remain explicitly opt-in. Passing loopback tests proves exporter behavior and payload shape, not arrival in production data-x tables.

## CI and LLM Agent Detection

dotnetup uses the same CI environment detection and LLM agent detection as the
.NET SDK. For details on which environment variables are checked, see the
.NET CLI telemetry documentation:
https://aka.ms/dotnet-cli-telemetry

## Privacy

Protecting your privacy is important to Microsoft. If you suspect the telemetry
is collecting sensitive data or the data is being insecurely or inappropriately
handled, file an issue in the dotnet/sdk repository:
https://github.com/dotnet/sdk/issues

For more information, see the Microsoft Privacy Statement:
https://www.microsoft.com/privacy/privacystatement

## See Also

- .NET CLI telemetry: https://aka.ms/dotnet-cli-telemetry
- dotnetup source code: https://github.com/dotnet/sdk/tree/release/dnup/src/Installer/dotnetup
