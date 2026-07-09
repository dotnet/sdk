---
name: add-cli-command
description: >
  Add or change a dotnet CLI command, subcommand, or option across the relevant CLI projects.
  USE FOR: adding or changing a dotnet CLI command/subcommand, adding a new
  Option<T> or Argument<T>, registering a subcommand in the command tree, changing
  an option's help description or a command's runtime message, wiring a command
  parser, or updating --help output.
license: MIT
---

# Add or change a dotnet CLI command or option

See [`references/file-map.md`](references/file-map.md) for the exact file map and the
two-resx reference table. Read it before editing.

## Add an option to an existing command

1. **Declare** the option in
   `src/Cli/Microsoft.DotNet.Cli.Definitions/Commands/<Area>/<Name>CommandDefinition.cs`.
   Prefer a `CommonOptions` factory (`Common/CommonOptions.cs`) when one exists:

   ```csharp
   public readonly Option<bool> NoLogoOption = CommonOptions.CreateNoLogoOption();
   // or, declared inline:
   public readonly Option<string> OutputOption = new Option<string>("--output", "-o")
   {
       Description = CommandDefinitionStrings.BuildOutputOptionDescription
   };
   ```

   Add it to the command in the constructor: `Options.Add(OutputOption);`.
2. **Consume** it in the managed implementation under
   `src/Cli/dotnet/Commands/<Area>/...` via `definition.OutputOption`; runtime
   messages come from `CliCommandStrings.<Key>`.
3. **Strings**: add the help description to `CommandDefinitionStrings.resx` and any
   runtime message to `CliCommandStrings.resx`, then regenerate the `.xlf` siblings.
4. **Test**: add parser/validation tests under
   `test/dotnet.Tests/CommandTests/<Area>/` and update any Verify help snapshots.

## Add a new command

Everything above, plus:

1. Create `Commands/<Area>/<Name>CommandDefinition.cs` in the Definitions project.
2. Register it in the root tree
   (`Commands/DotNetCommandDefinition.cs` constructor):

   ```csharp
   Subcommands.Add(<Name>Command = new());
   ```
3. Create the managed implementation and a `<Name>CommandParser.cs` exposing
   `ConfigureCommand(...)` under `src/Cli/dotnet/Commands/<Name>/`.
4. Wire the parser in `src/Cli/dotnet/Parser.cs` `ConfigureManagedActions`:

   ```csharp
   <Name>CommandParser.ConfigureCommand(rootCommand.<Name>Command);
   ```

> **Native AOT:** a newly registered command already *runs* under Native AOT via the
> managed fallback (the `dotnet-aot` host runs `dotnet.dll` through hostfxr), and its
> parsing and `--help` reach the AOT CLI for free. Making the command execute
> **in-process** in the native binary is separate, deliberate work — follow the
> **add-dotnet-aot-command** skill when AOT support is required.

## Checklist

- [ ] Option/command declared in `Definitions`, registered in its parent's constructor.
- [ ] Managed behavior wired in `src/Cli/dotnet` and (for new commands) in `Parser.cs`.
- [ ] Help description in `CommandDefinitionStrings.resx`; runtime message in `CliCommandStrings.resx`.
- [ ] `.xlf` files regenerated via `/t:UpdateXlf` (not hand-edited), resx + xlf committed together.
- [ ] No heavy dependency leaked into `Definitions`.
- [ ] For in-process AOT execution, followed **add-dotnet-aot-command** (managed fallback applies otherwise).
- [ ] Tests added/updated; Verify `*.verified.txt` snapshots promoted if CLI output changed.
