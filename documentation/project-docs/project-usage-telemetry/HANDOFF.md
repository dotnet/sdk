# Project usage telemetry: implementation handoff

This file records the current state of the work, so that a new session can continue it. Read it together with [design.md](design.md), which is the approved design. If the two files disagree, the **final decisions** below take priority.

## Requirements (summary)
- On every build, record these values for each user project:
  - target frameworks
  - runtime identifiers
  - project type (the extension)
  - file counts by extension
  - compiler reference names, without `.dll`
  - SDK version
- Aggregate the data per user, per UTC day, and per project, in a dedicated subfolder of the dotnet CLI home.
- A hidden command exports the data. The dotnet CLI starts it in the background.
  - The command exits right away when the configured interval has not passed.
  - It takes an exclusive lock, and returns success as a no-op when another export holds the lock.
  - It sends one aggregate event for each project and day.
- Everything must work with Native AOT and must have good test coverage.

## Final decisions (they replace parts of design.md)
- **Store backends:** implement JSON first; binary is optional. **SQLite is deferred**, because the repo has no SQLite package and adding one has costs for source-build and native RIDs.
- **Layout:** `<DotnetUserProfileFolderPath>/project-usage-telemetry/<json|binary>/<yyyy-MM-dd>/<projectId>.<ext>`.
  - Shared files go at the `project-usage-telemetry` root:
    - `.last-export`: the sentinel.
    - `.export.lock`: the export lock.
    - `.store.lock`: the store lock. Open it with `FileShare.None` and retry for about 5 seconds.
  - Do **not** use `DeleteOnClose`, because it races on Unix.
  - Write files atomically with a temp file and `File.Move(overwrite)`.
  - Store a `SchemaVersion` value in every file.
- **Task-to-logger encoding:** use the string properties of the `projectusage` event. The format is implemented in `CollectProjectUsageTelemetry.cs`:
  - `ProjectId`: SHA-256 hex of the uppercased full path.
  - `ProjectType`: the lowercase extension.
  - `TargetFramework`, `RuntimeIdentifier`, `SdkVersion`.
  - `RuntimeIdentifiers`: sorted, joined with `;`.
  - `References`: sorted, joined with `;`.
    - Names with `FrameworkReferenceName` or `NuGetPackageId` metadata stay in clear text.
    - Other names are replaced by the full SHA-256 of the uppercased name.
  - `Files`: `.ext=h1,h2;...`.
    - Each hash is the first 8 bytes of SHA-256 of the normalized relative path (uppercased, `/` separators).
    - Files with no extension go under `(none)`.
    - Paths are deduplicated across item types.
- **Collection gate:** the target runs only when `$(DOTNET_CLI_TELEMETRY_SESSIONID)` is set. The CLI sets that variable only when telemetry is enabled, so builds from Visual Studio or from `msbuild.exe` skip the target.
  - Design-time builds also skip it.
  - Users can opt out with `GenerateProjectUsageTelemetry=false`.
- **Export event:** `projectusage/daily`, for **completed UTC days only**. Properties:
  - Day, ProjectId, ProjectType, TargetFrameworks, RuntimeIdentifiers, SdkVersions, References, BuildCount, SchemaVersion.
  - FileCounts, in the form `.cs=12;.razor=3`. Only counts are exported; the file hashes stay on the local disk.

## Status

### Done (in this commit)
| File | State |
|---|---|
| `src/Tasks/Microsoft.NET.Build.Tasks/CollectProjectUsageTelemetry.cs` | Complete. Builds for net11.0 and net472. |
| `src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.TargetFrameworkInference.targets` | `UsingTask`, plus the `_CollectProjectUsageTelemetry` target (AfterTargets=CoreCompile). |
| `test/Microsoft.NET.Build.Tasks.Tests/GivenACollectProjectUsageTelemetryTask.cs` | 2 passing tests. Add a test for duplicate files and grouping by extension. |
| `src/Common/EnvironmentVariableNames.cs` | 5 env var names (see below). |
| `src/Cli/dotnet/Telemetry/ProjectUsage/IProjectUsageStore.cs` | Store interface. **It refers to `ProjectUsageRecord`, which does not exist yet, so `dotnet.csproj` does not compile until that type is added.** |

Env vars:
- `DOTNET_CLI_PROJECT_USAGE_STORE`: `json` or `binary`.
- `DOTNET_CLI_PROJECT_USAGE_EXPORT_INTERVAL_HOURS`: default 24.
- `DOTNET_CLI_PROJECT_USAGE_RETENTION_DAYS`: default 30.
- `DOTNET_CLI_PROJECT_USAGE_EXPORT_SPAWNED`: recursion guard.
- `DOTNET_CLI_DISABLE_PROJECT_USAGE_EXPORT`: kill switch.

### Remaining work (in this order)
1. **Model**, in `src/Cli/dotnet/Telemetry/ProjectUsage/`. Namespace: `Microsoft.DotNet.Cli.Telemetry.ProjectUsage`.
   - `ProjectUsageRecord` fields:
     - `ProjectId`, `ProjectType`.
     - Ordinal sorted sets: `TargetFrameworks`, `RuntimeIdentifiers`, `SdkVersions`, `References`.
     - `FileHashesByExtension`: extension → set of hashes.
     - `BuildCount`.
     - `MergeFrom(other)`: union of sets, and the sum of build counts.
   - A JSON DTO and a source-generated `JsonSerializerContext`.
2. **Parser:** `ProjectUsageEventParser`. It turns the event properties into a `ProjectUsageRecord`.
3. **Stores:**
   - `FileProjectUsageStore` (abstract), with `JsonProjectUsageStore` and an optional `BinaryProjectUsageStore`.
   - `ProjectUsageStoreFactory`, which reads the env var. JSON is the default.
   - Testable constructor parameters: root directory, env getter, clock.
   - Use `src/Cli/dotnet/SdkVulnerability/SdkReleaseMetadataCache.cs` as the example for the sentinel, the atomic write, and the JSON context.
4. **Logger:** in `src/Cli/dotnet/Commands/MSBuild/MSBuildLogger.cs`:
   - In `OnTelemetryLogged`, take out `projectusage` events before `FormatAndSend`. Parse each one and buffer it per ProjectId, under a lock.
   - In `OnBuildFinished`, call `store.Merge(utcToday, buffer)` inside try/catch, then clear the buffer, because the MSBuild server reuses the process.
   - Add a constructor `(ITelemetryClient, IProjectUsageStore?, Func<DateTime> utcNow)`.
5. **Exporter:** `ProjectUsageExporter` and `IProjectUsageEventSink`. Steps:
   1. When telemetry is off, return 0.
   2. When the sentinel is still fresh, return 0.
   3. Take the lock. When the lock is busy, return 0.
   4. Check the sentinel again.
   5. Send the completed days and delete them.
   6. Remove data that is older than the retention period.
   7. Touch the sentinel, flush, and return 0.

   The production sink wraps `TelemetryClient.Instance.ThreadBlockingTrackEvent` inside a child `Activity`.
6. **Hidden command:** `internal-export-project-usage`.
   - Definition: `src/Cli/Microsoft.DotNet.Cli.Definitions/Commands/Hidden/InternalExportProjectUsage/`. Follow the `InternalReportInstallSuccess` example.
   - Register the definition in `DotNetCommandDefinition.cs`.
   - Put the parser and command in `src/Cli/dotnet/Commands/Hidden/InternalExportProjectUsage/`.
   - In `Parser.cs`, wire it in **both** `ConfigureManagedActions` and `ConfigureAotActions`.
7. **Launcher:** `ProjectUsageExportLauncher`.
   - `ShouldLaunch` skips the launch in these cases:
     - There are no args, or the first arg starts with `-`.
     - The first arg is `help`, `complete`, or `[suggest`, or starts with `internal-`.
     - Telemetry is off.
     - The recursion guard or the kill switch is set.
     - The sentinel is not due yet.
   - It starts `host <sdkDir>/dotnet.dll internal-export-project-usage` without waiting for it, and it never throws.
   - Call it from these two places:
     - the `finally` block of `Program.Main`, with `Environment.ProcessPath` and `AppContext.BaseDirectory`;
     - the `finally` block of `NativeEntryPoint.ExecuteCore`, only when `aotHandledInProcess` is true.
8. **AOT:** link every new CLI file in `src/Cli/dotnet-aot/AotSourceFiles.props`. Code that does not work with AOT goes inside `#if !CLI_AOT`.
9. **Tests:**
   - In `test/dotnet.Tests`:
     - store contract tests, run against each backend;
     - tests for the parser, the exporter (lock, sentinel, completed days only, opt-out), the launcher decisions, and the logger.
   - In `test/dotnet-aot.Tests/AotParserTests.cs`: a parser test for the new command.
   - An integration test: build a multi-TFM asset with a temp `DOTNET_CLI_HOME`.
10. **Docs:** update `documentation/project-docs/telemetry.md` and `src/Cli/AGENTS.md`.

## How to build and test (findings so far)
- Don't run `build.cmd` for routine work. Run `.\restore.cmd` once, then build single projects with `.\.dotnet\dotnet build <csproj> /bl:N.binlog`.
- The repo uses Microsoft.Testing.Platform, so `dotnet test --filter` can find 0 tests. Run the test DLL directly instead:
  `.\.dotnet\dotnet exec artifacts\bin\Microsoft.NET.Build.Tasks.Tests\Debug\net11.0\Microsoft.NET.Build.Tasks.Tests.dll --filter "FullyQualifiedName~ProjectUsage"`
- Code in the task project must also compile for net472. Don't use `Path.GetRelativePath`, `SHA256.HashData`, or `Convert.ToHexStringLower`.
