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

## Where things live

`src/Cli` is a set of projects, not one app. The three above carry commands; the
rest are supporting libraries:

| Project | Role |
|---------|------|
| `dotnet` | Primary managed executable — every command's handler lives here under `Commands/`. |
| `Microsoft.DotNet.Cli.Definitions` | AOT-safe command tree (parsed by both hosts). |
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
