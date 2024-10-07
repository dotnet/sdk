## CLI Guidelines for the dotnet CLI and related commands

The `dotnet` CLI is the entrypoint for the .NET experience for all users, and as such it needs to be usable, performance, and consistent.
This document outlines a series of UX guidelines, CLI grammars, and behavioral expectations for the `dotnet` CLI and related commands so that
users have consistent experiences with all `dotnet` commands - even those that do not ship directly with the SDK like dotnet tools.

- [CLI Guidelines for the dotnet CLI and related commands](#cli-guidelines-for-the-dotnet-cli-and-related-commands)
- [Common Options](#common-options)
  - [Verbosity](#verbosity)
  - [Framework selection](#framework-selection)
  - [RID selection](#rid-selection)
  - [MSBuild Properties](#msbuild-properties)
  - [Output modes/formatting](#output-modesformatting)
- [NuGet-related options](#nuget-related-options)
  - [NuGet.Config File selection](#nugetconfig-file-selection)
  - [Package source management](#package-source-management)
- [Contextual behaviors](#contextual-behaviors)
  - [Implicit project/solution file discovery](#implicit-projectsolution-file-discovery)
  - [Interactive session sniffing](#interactive-session-sniffing)
- [Interaction patterns](#interaction-patterns)
  - [support structured (JSON) output](#support-structured-json-output)
  - [StdOut/StdErr usage](#stdoutstderr-usage)
- [References](#references)

## Common Options

These options are present on a (sub)set of commands, and should be consistent in their behavior and usage regardless of the context in which they are used. Users expect these kinds of options to be generally available and will often unconsciously reach for them as best guesses/first attempts.

### Verbosity

Short Form: `-v`

Long Form: `--verbosity`

Allowed values: `[q]uiet`, `[m]inimal`, `[n]ormal`, `[d]etailed`, `[diag]nostic`

The CLI uses MSBuild verbosity level names. Commands should at minimum have support for three of these:
* `quiet` mode should output nothing except the specific data output of the command.
* `normal` mode should include high-priority messages in addition to the output
* `diagnostic` mode should include all messages, including low-priority/debug/verbose messages.

If you only support a subset of these values, map the missing ones to the closest semantic match. For example, if you don't support `minimal`, map it to `quiet`. If you don't support `detailed`, map it to `diagnostic`.

### Framework selection

### RID selection
### MSBuild Properties
### Output modes/formatting

Long form: `--output`
Allowed values: `text`, `json`, others are relevant for your use case

Users value scriptability of CLI commands, and some form of structured output is key to supporting this. JSON is a common structured output format, but other formats may be more appropriate for your use case. If you use a structured format like `csv`, please for the love of Turing use a proper CSV writer and not just a hand-rolled comma-separated list of values so that you don't break [RFC 4180][4180].

## NuGet-related options

### NuGet.Config File selection
### Package source management

## Contextual behaviors

### Implicit project/solution file discovery
### Interactive session sniffing

## Interaction patterns

### support structured (JSON) output
### StdOut/StdErr usage


## References

[Command-line interface guidelines][clig]

[clig]: https://clig.dev/
[4180]: https://www.loc.gov/preservation/digital/formats/fdd/fdd000323.shtml
