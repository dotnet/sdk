# File map: CLI command/option changes

Reference tables for the `add-cli-command` skill. All paths are verified against the
repo layout.

## Add an option — files to touch

| Step | File |
|------|------|
| Declare `Option<T>` + `Options.Add(...)` in ctor | `src/Cli/Microsoft.DotNet.Cli.Definitions/Commands/<Area>/<Name>CommandDefinition.cs` |
| Shared option factory (preferred) | `src/Cli/Microsoft.DotNet.Cli.Definitions/Common/CommonOptions.cs` |
| Help description string | `src/Cli/Microsoft.DotNet.Cli.Definitions/CommandDefinitionStrings.resx` |
| Consume option in handler | `src/Cli/dotnet/Commands/<Area>/...` |
| Runtime message string | `src/Cli/dotnet/Commands/CliCommandStrings.resx` |
| Regenerate localization | `.xlf` siblings of both resx (via `/t:UpdateXlf`) |
| Parser/validation tests | `test/dotnet.Tests/CommandTests/<Area>/` |
| Help snapshot (if `--help` output changes) | `*.verified.txt` next to the snapshot test |

## Add a command — additional files

| Step | File |
|------|------|
| New command definition class | `src/Cli/Microsoft.DotNet.Cli.Definitions/Commands/<Area>/<Name>CommandDefinition.cs` |
| Register in the root tree (`Subcommands.Add(<Name>Command = new());`) | `src/Cli/Microsoft.DotNet.Cli.Definitions/Commands/DotNetCommandDefinition.cs` |
| Managed impl + `<Name>CommandParser.cs` (`ConfigureCommand(...)`) | `src/Cli/dotnet/Commands/<Name>/` |
| Wire parser (`<Name>CommandParser.ConfigureCommand(rootCommand.<Name>Command);`) | `src/Cli/dotnet/Parser.cs` → `ConfigureManagedActions` |

## Native AOT

Parsing and `--help` reach the AOT CLI for free via `Definitions`, and a newly
registered command runs under AOT through the managed fallback. To make a command
execute **in-process** in the Native AOT binary — the `dotnet-aot`/`dn` source closure,
`AotSourceFiles.props`, `#if CLI_AOT` gating, and the AOT tests — follow the
**add-dotnet-aot-command** skill, which owns those file details.

## The two resx files

| Resx | Path | Holds | Localized via |
|------|------|-------|---------------|
| `CommandDefinitionStrings` | `src/Cli/Microsoft.DotNet.Cli.Definitions/CommandDefinitionStrings.resx` | **Help descriptions** for commands, options, and arguments (parsed by both managed and AOT hosts). | `xlf/CommandDefinitionStrings.<locale>.xlf` |
| `CliCommandStrings` | `src/Cli/dotnet/Commands/CliCommandStrings.resx` | **Runtime messages** emitted by managed handlers (errors, status, validation). | `Commands/xlf/CliCommandStrings.<locale>.xlf` |
