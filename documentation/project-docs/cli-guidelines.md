## CLI Guidelines for the dotnet CLI and related commands

The `dotnet` CLI is the entrypoint for the .NET experience for all users, and as such it needs to be usable, performance, and consistent.
This document outlines a series of UX guidelines, CLI grammars, and behavioral expectations for the `dotnet` CLI and related commands so that
users have consistent experiences with all `dotnet` commands - even those that do not ship directly with the SDK like dotnet tools.

- [CLI Guidelines for the dotnet CLI and related commands](#cli-guidelines-for-the-dotnet-cli-and-related-commands)
- [Common Options](#common-options)
  - [Verbosity](#verbosity)
  - [Framework selection](#framework-selection)
  - [RID selection](#rid-selection)
    - [Explicit RID](#explicit-rid)
    - [OS-specific RID](#os-specific-rid)
    - [Architecture-specific RID](#architecture-specific-rid)
    - [SDK-matching RID](#sdk-matching-rid)
  - [MSBuild Properties](#msbuild-properties)
  - [Output modes/formatting](#output-modesformatting)
- [NuGet-related options](#nuget-related-options)
  - [NuGet.Config File selection](#nugetconfig-file-selection)
  - [Package source management](#package-source-management)
    - [Additive package sources](#additive-package-sources)
    - [Exclusive package sources](#exclusive-package-sources)
  - [Feed Authentication support](#feed-authentication-support)
- [Contextual behaviors](#contextual-behaviors)
  - [Implicit project/solution file discovery](#implicit-projectsolution-file-discovery)
  - [Interactive session sniffing](#interactive-session-sniffing)
  - [Determining interactive sessions](#determining-interactive-sessions)
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

Short form: `-f <TFM>`

Long form: `--framework <TFM>`

### RID selection

#### Explicit RID

Short form: `-r <RID>`

Long form: `--runtime <RID>`

#### OS-specific RID

Short form: `-o <OS>`

Long form: `--os <OS>`

#### Architecture-specific RID

Short form: `-a <ARCH>`

Long form: `--arch <ARCH>`

#### SDK-matching RID

Short form: `--ucr`

Long form: `--use-current-runtime`

### MSBuild Properties

Short form: `-p <MSBuild property expression(s)>`

Long form: `--property <MSBuild property expression(s)>`

If at all possible we _strongly encourage_ not parsing the `<MSBuild property expression(s)>` syntax yourself. It is much more complex than you think it is. At _best_ you should detect and forward along any of these arguments to any MSBuild invocations you make.

### Output modes/formatting

Long form: `--output`

Allowed values: `text`, `json`, others are relevant for your use case

Users value scriptability of CLI commands, and some form of structured output is key to supporting this. JSON is a common structured output format, but other formats may be more appropriate for your use case. If you use a structured format like `csv`, please for the love of Turing use a proper CSV writer and not just a hand-rolled comma-separated list of values so that you don't break [RFC 4180][4180].

When you write JSON outputs you are explicitly creating a data contract with users. Be _very intentional_ about changes to the format of this contract. Design up-front for extensibility. For example, instead of just emitting a list of versions as an array of strings, consider emitting them as an array of objects with a named property: `[{"workload_set_version": "1.0.0"}]` instead of `"1.0.0"`.

## NuGet-related options

### NuGet.Config File selection

Long form: `--config-file`
Default value: directory-based probing implemented in the [`NuGet.Configuration`][nuget-configuration] library

### Package source management

There are two semantics of behaviors that we have adopted in the dotnet CLI: additive and exclusive package sources.

#### Additive package sources

Long form: `--add-source <source_uri>`

When this is used, you should load the sources list from the NuGet configuration (using the [libraries][nuget-configuration]) and create a new `PackageSource` to add to that set. This should be additive and not destructive.

#### Exclusive package sources

Long form: `--source <source_uri>`

When this is used, you should ONLY use the sources supplied by this parameter and ignore any sources in the NuGet configuration. This is useful for scenarios where you want to ensure that you're only using a specific source for a specific operation.

### Feed Authentication support

Long form: `--interactive <bool>`
Default value: `true` when in an [interactive session](#determining-interactive-sessions), `false` otherwise

NuGet authentication often requires some interactive action like going to a browser page. You should 'prime' the NuGet credential services by calling `NuGet.Credentials.DefaultCredentialServiceUtility.SetupDefaultCredentialService(ILogger logger, bool nonInteractive)` with the value of `nonInteractive` being the inverted value of this parameter. This will ensure that the credential service is set up correctly for the current session type.

## Contextual behaviors

### Implicit project/solution file discovery
### Interactive session sniffing
### Determining interactive sessions

## Interaction patterns

### support structured (JSON) output
### StdOut/StdErr usage

## References

[Command-line interface guidelines][clig]

[clig]: https://clig.dev/
[4180]: https://www.loc.gov/preservation/digital/formats/fdd/fdd000323.shtml
[nuget-configuration]: https://nuget.org/packages/NuGet.Configuration
