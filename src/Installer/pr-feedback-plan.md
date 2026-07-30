# PR 55421 Feedback Plan

## Summary

- Total review threads: 11
- Already resolved before this pass: 5
- Addressed in this pass: 6
- Quick fixes: 3
- Medium fixes: 2
- Investigation items: 1
- Status: all feedback addressed

## Already Resolved

| # | File | Comment | Link | Status |
|---|---|---|---|---|
| R1 | `PersistentStorageTelemetryUploader.cs` | Avoid poison-blob head-of-line blocking. | [r3641979795](https://github.com/dotnet/sdk/pull/55421#discussion_r3641979795) | Resolved before this pass. |
| R2 | `DotnetupTelemetryDrainProcess.cs` | Clarify the best-effort exit-code contract. | [r3641979814](https://github.com/dotnet/sdk/pull/55421#discussion_r3641979814) | Resolved before this pass. |
| R3 | `CliFolderPathCalculatorCore.cs` | Explain the newer API use required by dotnetup. | [r3641996703](https://github.com/dotnet/sdk/pull/55421#discussion_r3641996703) | Resolved before this pass. |
| R4 | `DotnetupTelemetry.cs` | Consider the legacy `print-env-script` alias. | [r3642068409](https://github.com/dotnet/sdk/pull/55421#discussion_r3642068409) | Resolved by design; daily-build compatibility is intentionally not retained. |
| R5 | `PersistentStorageTelemetryBackgroundWorkerTests.cs` | Initialize the nullable `TestContext` property. | [r3642068437](https://github.com/dotnet/sdk/pull/55421#discussion_r3642068437) | Resolved before this pass. |

## Addressed Feedback

### Q1: Add the telemetry project to the dotnetup solution filter

**Link:** [r3667565066](https://github.com/dotnet/sdk/pull/55421#discussion_r3667565066)

**Comment:** "`Microsoft.DotNet.Cli.Telemetry.csproj` should be added to `dotnetup.slnf`."

**Status:** Done - added the referenced telemetry project to the solution filter.

**Code:** [dotnetup.slnf L5](../../dotnetup.slnf#L5)

### Q2: Rename the rejected-result factory

**Link:** [r3668935618](https://github.com/dotnet/sdk/pull/55421#discussion_r3668935618)

**Comment:** "`RejectedAfter` is misleading; use a name that makes the retry delay explicit."

**Status:** Done - renamed it to `RejectedRetryAfter` and updated all callers and tests.

**Code:** [TelemetryUploadResult.cs L61](../Cli/Microsoft.DotNet.Cli.Telemetry/Implementation/TelemetryUploadResult.cs#L61)

### Q3: Clarify the drain-pass progress result

**Link:** [r3668948137](https://github.com/dotnet/sdk/pull/55421#discussion_r3668948137)

**Comment:** "The meaning of `ForwardProgress` is unclear."

**Status:** Done - renamed it to `DeletedBlobCount` and documented exactly which source-blob removals it counts.

**Code:** [TelemetryUploadResult.cs L78-L84](../Cli/Microsoft.DotNet.Cli.Telemetry/Implementation/TelemetryUploadResult.cs#L78-L84)

### M1: Replace assertion-style telemetry failure reporting

**Link:** [r3669231424](https://github.com/dotnet/sdk/pull/55421#discussion_r3669231424)

**Comment:** "Confirm whether `Debug.Fail` is appropriate in the detached process and make local failures observable."

**Status:** Done - replaced assertion-triggering `Debug.Fail` calls in the uploader and standalone drainer with non-asserting `Debug.WriteLine` diagnostics. Telemetry remains best-effort and never affects process success.

**Code:** [PersistentStorageTelemetryUploader.cs L132](../Cli/Microsoft.DotNet.Cli.Telemetry/Implementation/PersistentStorageTelemetryUploader.cs#L132), [PersistentStorageTelemetryDrainer.cs L115](../Cli/Microsoft.DotNet.Cli.Telemetry/PersistentStorageTelemetryDrainer.cs#L115)

### M2: Use a hidden raw argument for drainer activation

**Link:** [r3669282002](https://github.com/dotnet/sdk/pull/55421#discussion_r3669282002)

**Comment:** "Prefer a hidden command/raw argument over an environment variable."

**Status:** Done - replaced the inherited environment-variable mode with the exact internal `--drain-telemetry` argument. It is still handled before parser, encoding, first-run, and telemetry initialization, and additional arguments do not enter drain mode.

**Code:** [Constants.cs L81-L84](dotnetup.Library/Constants.cs#L81-L84), [Program.cs L20-L23](dotnetup.Library/Program.cs#L20-L23), [DotnetupTelemetryDrainProcess.cs L20-L31](dotnetup.Library/Telemetry/DotnetupTelemetryDrainProcess.cs#L20-L31)

### L1: Ground retry timing in documented production behavior

**Link:** [r3669284620](https://github.com/dotnet/sdk/pull/55421#discussion_r3669284620)

**Comment:** "Replace imaginary retry-delay constants with values supported by Application Insights guidance or a production implementation."

**Status:** Done - based fallback retries on the official Azure Monitor OpenTelemetry exporter's storage retry policy: jittered exponential backoff with a 10-second floor. The one-minute fallback cap matches Azure.Core. Server `Retry-After` values are honored directly, and a delay beyond the remaining drainer lifetime exits immediately so a future process can retry the persisted blob.

**Code:** [PersistentStorageTelemetryDrainer.cs L32-L44](../Cli/Microsoft.DotNet.Cli.Telemetry/PersistentStorageTelemetryDrainer.cs#L32-L44), [PersistentStorageTelemetryDrainer.cs L126-L143](../Cli/Microsoft.DotNet.Cli.Telemetry/PersistentStorageTelemetryDrainer.cs#L126-L143), [PersistentStorageTelemetryDrainer.cs L209-L214](../Cli/Microsoft.DotNet.Cli.Telemetry/PersistentStorageTelemetryDrainer.cs#L209-L214), [PersistentStorageTelemetryDrainerTests.cs L62-L82](../../test/Microsoft.DotNet.Cli.Telemetry.Tests/PersistentStorageTelemetryDrainerTests.cs#L62-L82)

## Files Modified

- `dotnetup.slnf`
- `src/Cli/Microsoft.DotNet.Cli.Telemetry/Implementation/HttpTelemetryUploadTransport.cs`
- `src/Cli/Microsoft.DotNet.Cli.Telemetry/Implementation/PersistentStorageTelemetryUploader.cs`
- `src/Cli/Microsoft.DotNet.Cli.Telemetry/Implementation/TelemetryUploadResult.cs`
- `src/Cli/Microsoft.DotNet.Cli.Telemetry/PersistentStorageTelemetryDrainer.cs`
- `src/Installer/dotnetup.Library/Constants.cs`
- `src/Installer/dotnetup.Library/Program.cs`
- `src/Installer/dotnetup.Library/Telemetry/DotnetupTelemetryDrainProcess.cs`
- `test/Microsoft.DotNet.Cli.Telemetry.Tests/PersistentStorageTelemetryDrainerTests.cs`
- `test/Microsoft.DotNet.Cli.Telemetry.Tests/PersistentStorageTelemetryUploaderTests.cs`
- `test/dotnetup.Tests/DotnetupTelemetryDrainProcessTests.cs`
- `test/dotnetup.Tests/TelemetryDrainE2ETests.cs`
- `test/dotnetup.Tests/Utilities/TelemetryTestEnvironment.cs`

## Build and Test Status

- dotnetup NativeAOT build: passed
- dotnetup drainer unit tests: 6 passed
- telemetry uploader, transport, and drainer tests: 29 passed
- native `--drain-telemetry` fast-path smoke test: exit code 0
