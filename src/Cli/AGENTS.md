# CLI Agent Instructions

Guidance for changes under `src/Cli`.

## SDK process entry points

SDK-owned CLI code has three process entry points of equal importance:

| Entry point | Source | Host and lifecycle |
|-------------|--------|--------------------|
| Managed CLI | `dotnet/Program.cs` | CoreCLR calls `Program.Main`. The process ends after the command completes. |
| Native AOT CLI | `dotnet-aot/NativeEntryPoint.cs` | The native host calls the exported `dotnet_execute`. Unsupported operations can continue in the managed CLI. |
| MSBuild logger | `dotnet/Commands/MSBuild/MSBuildLogger.cs` | MSBuild loads the SDK assembly as an `INodeLogger`. `MSBuildForwardingApp` adds the `-distributedlogger` argument. The logger can run in the CLI process, a child process, or a persistent server. |

Treat the logger as an independent entry point. Do not assume that it runs after managed
`Program.Main` or the Native AOT bootstrap. Initialize process-wide telemetry and tracing
when MSBuild loads the logger directly. Start request-specific state at `BuildStarted`,
record the result at `BuildFinished`, and keep the activity open until `Shutdown` because
MSBuild emits final telemetry after `BuildFinished`. `Shutdown` completes one logger
instance. It does not necessarily end the process. Refresh the environment and trace
context for each persistent-server request.

Managed and Native AOT command execution use
`Microsoft.DotNet.Cli.Utils/ProcessLifecycle.cs` as the shared owner of Ctrl+C,
SIGTERM, and process-exit cancellation. The managed entry point initializes it for the
process lifetime; the Native AOT entry point initializes it only while handling a command
in-process and unregisters it before managed fallback. Merely launching a child through
`ProcessReaper.cs` must not initialize CLI lifecycle handling in library hosts.

Pass the lifecycle token through System.CommandLine invocation and command operations.
Process-launch and forwarding helpers must require callers to supply a cancellation
token; do not add tokenless overloads or optional cancellation-token parameters.
The first Ctrl+C requests cooperative cancellation and permits 200 milliseconds for
cleanup; expiration of that deadline requests forceful cancellation, gives observers a
bounded window to terminate and reap child processes, and then exits with the
signal-derived exit code. A second Ctrl+C or SIGTERM exits immediately. Transaction
rollback and other critical operations may use `OverrideSoftCancellationTimeout` to
extend or suspend
the automatic deadline, but the override must never suppress an explicit forceful signal.
Child processes observe cooperative signals directly and are killed only when forceful
cancellation is requested. While a lifecycle-managed external process is running,
`Command` suspends the automatic soft-cancellation deadline so the child can finish
gracefully; a second Ctrl+C or SIGTERM still forces termination. System.CommandLine may
wrap the lifecycle token before it reaches a command action, so process termination must
use lifecycle initialization state rather than token identity to distinguish lifecycle
cancellation from standalone library callers. On Unix, forward SIGTERM to the child
before bounded forceful termination.
`ProcessReaper.cs` owns child-process signal forwarding and cleanup; do not add a
competing command-line termination handler for those signals. MSBuild operations use a
two-second soft-cancellation override so submissions and loggers, including binary
loggers, can finish cancellation cleanup; a second Ctrl+C or SIGTERM still bypasses it.
MTP test execution suspends the automatic deadline while running test applications so
their own first-Ctrl+C handlers can report final results. Its cancellation manager must
observe the shared cooperative and forceful lifecycle tokens: cooperative cancellation
stops queueing new applications, while forceful cancellation kills all registered test
application process trees before the lifecycle exits.

### Cancellation and cleanup

Forward-operation cancellation must not prevent transaction rollback. MSI manifest and
pack rollback create a distinct cancellation scope inside each rollback callback, never
linked to the cancelled forward token; keep the original cancellation exception after
cleanup. The deterministic installer seams are exercised by
[`GivenMsiCancellation`](../../test/dotnet.Tests/CommandTests/Workload/Install/GivenMsiCancellation.cs).
Only roll back MSIs whose installation this transaction actually attempted; mark the
attempt before native execution so partial installs still roll back, but cancellation
before installation and already-installed no-ops cannot remove existing manifests.

`Console.In` synchronizes `ReadLineAsync` as well as `ReadLine`. The
[`run-api` command](dotnet/Commands/Run/Api/RunApiCommand.cs) uses
`Console.OpenStandardInputHandle()` with `FileStream`/`StreamReader` instead. Keep the
cancellable wait: Unix file I/O cannot interrupt an already blocked read, so at most one
runtime worker can remain until process exit. Keep its regression out of process with a
real open stdin pipe; see
[`RunApiCancellationTests`](../../test/dotnet.Tests/CommandTests/Run/RunApiCancellationTests.cs).
On Linux, its helper waits for `/proc/self/task/*/syscall` to report a blocked
`read(0, ...)` before cancelling; an incomplete task alone does not establish that the
runtime worker has started the read.

## Three-project command split

A `dotnet` command or option spans three cooperating projects:

- **`Microsoft.DotNet.Cli.Definitions`** — the AOT-safe command tree. Shared
  option factories live in `Common/CommonOptions.cs`.
- **`src/Cli/dotnet`** — the managed implementation: handlers, validation,
  MSBuild/NuGet integration, runtime messages.
- **`src/Cli/dotnet-aot`** + **`src/Cli/dn`** — the NativeAOT bridge (see
  `src/Cli/dotnet-aot/DESIGN.md`).

The same definition tree is parsed by both the managed and AOT hosts, so
parser/option/description changes flow to AOT and `--help` automatically. Keep heavy
deps out of `Definitions`. In the managed CLI, code that isn't AOT-safe is excluded
from the AOT build with `#if !CLI_AOT` (the AOT project links files from `dotnet` and
compiles with `CLI_AOT` defined).

`MSBuildRuntimeReference.targets` supplies the packaged MSBuild command-line assembly
to `dotnet` and `Microsoft.DotNet.Cli.Utils`. Internal parser APIs are accessible to the
signed `dotnet` assembly through MSBuild's `InternalsVisibleTo`; keep those calls
managed-only rather than introducing MSBuild dependencies into `Definitions`.

## Where things live

`src/Cli` is a set of projects, not one app. The three above carry commands; the
rest are supporting libraries:

| Project | Role |
|---------|------|
| `dotnet` | Primary managed executable — every command's handler lives here under `Commands/`. |
| `Microsoft.DotNet.Cli.Definitions` | AOT-safe command tree (parsed by both hosts). |
| `Microsoft.DotNet.Cli.InternalMicrosoft` | AOT-safe Microsoft-internal classification, probes, and cache. It does not emit telemetry. |
| `dotnet-aot` + `dn` | NativeAOT shared library + native host exe. |
| `Microsoft.DotNet.Cli.Utils` | MSBuild/NuGet/process/system abstractions used across the CLI. |
| `Microsoft.DotNet.Cli.CoreUtils` | Low-level version/file/env-variable parsing. |
| `Microsoft.DotNet.Cli.CommandLine` | Local extensions over `System.CommandLine`. |
| `Microsoft.DotNet.Configurer` | First-run experience and NuGet/config setup. |
| `Microsoft.DotNet.InternalAbstractions` | File-system/env abstractions for testability. |
| `Microsoft.DotNet.FileBasedPrograms` | Support for file-based programs. |
| `Microsoft.TemplateEngine.Cli` | `dotnet new` integration layer. |

### Inside `src/Cli/dotnet`

- `Program.cs` / `Parser.cs` — entry point and parser construction.
- `Commands/` — one folder per command (Build, Restore, New, Tool, Workload, …),
  plus `CliCommandStrings.resx` and `xlf/`.
- `CommandFactory/` — command resolution strategies.
- `BuildServer/` — MSBuild / VBCSCompiler / Razor build-server providers.
- `ToolPackage/`, `ToolManifest/`, `ShellShim/`, `NugetPackageDownloader/`,
  `NugetSearch/` — `dotnet tool` install/run plumbing.

### Inside `Microsoft.DotNet.Cli.Definitions`

- `Commands/` — one definition class per command; `DotNetCommandDefinition.cs` is the
  registry that imports them all.
- `Common/` — shared option/argument factories.
- `Help/` — help builder and localization.

## Verify (approval) snapshot tests

Many CLI tests use Verify (`[UsesVerify]` / VerifyMSTest):

- The expected output is checked in as
  `<Test>.<Method>[.<OS>].verified.txt`.
- On mismatch the runner writes a git-ignored `*.received.txt`. **Never commit
  `*.received.txt`.**
- When you intentionally change CLI output, promote the new `*.received.txt` over the
  matching `*.verified.txt`.
- Volatile lines (paths, timings, versions) are scrubbed via
  `settings.ScrubLinesContaining(...)` — scrub rather than hard-code them.

## MTP build preparation

[`MSBuildSession`](dotnet/MSBuildSession.cs) owns the single-node build and project collection
for targets MTP invokes directly: restore, device discovery, deployment and run-argument computation.
Honor explicit `-mt` / `-multiThreaded` values using MSBuild's precedence and environment defaults,
without adding automatic response-file discovery.

A strict MT session owns the process current directory until it ends; do not restore that directory
between MT requests. [`MSBuildUtility`](dotnet/Commands/Test/MTP/MSBuildUtility.cs) runs intervening
builds and restores out-of-process through
[`RestoringCommand`](dotnet/Commands/Restore/RestoringCommand.cs), using the captured invocation
directory so relative arguments and logs retain their meaning. Keep one coherent preparation binlog,
target-produced project state, and the session's success/failure cleanup. Cover these boundaries in
[`GivenDotnetTestProducesBinaryLog`](../../test/dotnet.Tests/CommandTests/Test/GivenDotnetTestProducesBinaryLog.cs).

Parent-process operations must also use the captured invocation directory: pass it through background
workload updates to both `global.json` discovery and NuGet configuration, and initialize absolute MTP
trace paths before starting the session. Changing only a child process's directory is insufficient.

## MTP device selection

In `--solution` runs,
[`device selection`](dotnet/Commands/Test/MTP/SolutionAndProjectUtility.cs) applies only to
MTP test applications (`IsTestingPlatformApplication`); `IsTestProject` is a VSTest concept and must not
opt a project in. App and library projects, such as MAUI heads, can import `ComputeAvailableDevices` in
their inner builds and must not prompt for a device. The single-project path evaluates before restore, so
it must not apply this NuGet-dependent gate.

## MTP process completion

[`TestApplicationHandler`](dotnet/Commands/Test/MTP/TestApplicationHandler.cs) distinguishes
TestHost completion from orchestrator-only completion. An exit-0 `RefreshMappingsOrchestrator`
in collection mode or `RunAffectedTestsOrchestrator` in affected-run mode can finish without
a TestHost; legacy collection can omit handshakes entirely. Missing/unknown capabilities,
controller-only or mixed partial handshakes, and explicit handshake rejections must not
gain that exemption. Rejection and partial-handshake state must survive later accepted
handshakes. Keep these boundaries covered by
[`TestApplicationHandlerTests`](../../test/dotnet.Tests/CommandTests/Test/TestApplicationHandlerTests.cs).
