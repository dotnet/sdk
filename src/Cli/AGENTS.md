# CLI Agent Instructions

Guidance for changes under `src/Cli`.

## Three-project split

A `dotnet` command or option spans three cooperating projects:

- **`Microsoft.DotNet.Cli.Definitions`** — the AOT-safe command tree. Shared
  option factories live in `Common/CommonOptions.cs`.
- **`src/Cli/dotnet`** — the managed implementation: handlers, validation,
  MSBuild/NuGet integration, runtime messages.
- **`src/Cli/dotnet-aot`** + **`src/Cli/dn`** — the NativeAOT bridge (see
  `src/Cli/dotnet-aot/DESIGN.md`).

The same definition tree is parsed by both the managed and AOT hosts, so
parser/option/description changes flow to AOT and `--help` automatically. Keep heavy
deps out of `Definitions` and behind `#if !CLI_AOT` / `[RequiresDynamicCode]`.

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
