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
  - [Determining interactive sessions](#determining-interactive-sessions)
- [Interaction patterns](#interaction-patterns)
  - [Support structured (JSON) output](#support-structured-json-output)
  - [StdOut/StdErr usage](#stdoutstderr-usage)
  - [Advanced terminal output capabilities](#advanced-terminal-output-capabilities)
  - [Disabling advanced terminal output capabilities](#disabling-advanced-terminal-output-capabilities)
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

If you only support a subset of these values, map the missing ones to the closest semantic match.
For example, if you don't support `minimal`, map it to `quiet`. If you don't support `detailed`, map it to `diagnostic`.

For feedback on when to write to stderr or stdout, see the [StdOut/StdErr usage](#stdoutstderr-usage) section.
For guidance on laying out output for users, see the [Advanced terminal output capabilities](#advanced-terminal-output-capabilities) section.

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

If at all possible we _strongly encourage_ not parsing the `<MSBuild property expression(s)>` syntax yourself. It is much more complex than you think it is.
At _best_ you should detect and forward along any of these arguments to any MSBuild invocations you make.

### Output modes/formatting

Long form: `--format`

Allowed values: `text`, `json`, others are relevant for your use case

Users value scriptability of CLI commands, and some form of structured output is key to supporting this. JSON is a common structured output format,
but other formats may be more appropriate for your use case. If you use a structured format like `csv`, please for the love of Turing use a proper
CSV writer and not just a hand-rolled comma-separated list of values so that you don't break [RFC 4180][4180].

When you write JSON outputs you are explicitly creating a data contract with users. Be _very intentional_ about changes to the format of this contract.
Design up-front for extensibility. For example, instead of just emitting a list of versions as an array of strings, consider emitting them as an
array of objects with a named property: `[{"workload_set_version": "1.0.0"}]` instead of `"1.0.0"`.

## NuGet-related options

NuGet is key to delivering package management and security functionality to end users. We should strive to make the NuGet experience as consistent
as possible across all `dotnet` commands, by following the same patterns and behaviors.

### NuGet.Config File selection

Long form: `--config-file`

Default value: directory-based probing implemented in the [`NuGet.Configuration`][nuget-configuration] library

### Package source management

There are two semantics of behaviors that we have adopted in the dotnet CLI: additive and exclusive package sources.

#### Additive package sources

Long form: `--add-source <source_uri>`

When this is used, you should load the sources list from the NuGet configuration (using the [libraries][nuget-configuration]) and create a
 new `PackageSource` to add to that set. This should be additive and not destructive.

#### Exclusive package sources

Long form: `--source <source_uri>`

When this is used, you should ONLY use the sources supplied by this parameter and ignore any sources in the NuGet configuration. This is useful
for scenarios where you want to ensure that you're only using a specific source for a specific operation.

### Feed Authentication support

Long form: `--interactive <bool>`

Default value: `true` when in an [interactive session](#determining-interactive-sessions), `false` otherwise

NuGet authentication often requires some interactive action like going to a browser page. You should 'prime' the NuGet credential services by calling
`NuGet.Credentials.DefaultCredentialServiceUtility.SetupDefaultCredentialService(ILogger logger, bool nonInteractive)` with the value of `nonInteractive`
being the inverted value of this parameter. This will ensure that the credential service is set up correctly for the current session type.

## Contextual behaviors

### Implicit project/solution file discovery

When a command is run in a directory that contains a project or solution file, the command should automatically use that file as the
target for the command.
If the command is run in a directory that contains multiple project or solution files, the command should fail with an error message
indicating that the user should specify which file to use.

Consider the use of an option like `--project <path to project or solution>` to allow for executing the command against a specific
project or solution file at an arbitrary location - but be aware that many mechanisms in .NET are hierarchical from Current Working
Directory and may have different behaviors when used in this detached mode.

### Determining interactive sessions

Commands should be able to determine if they are running in an interactive session or not. This is important for determining

* whether to prompt the user for input or not,
* what the default value for NuGet's `--interactive` option should be
* how to format output (along with terminfo probing)

In general the easiest way to check this is if the `stdin` file descriptor is a TTY, or in .NET Terms: `Console.IsInputRedirected` is `false`.

## Interaction patterns

### Support structured (JSON) output

All commands should support structured (JSON) output. This is a key part of the CLI experience and allows for easier scripting and automation of the CLI.
See the [`--format`](#output-modesformatting) option for more details. When structured output is requested, the command should output the structured
data to `stdout` and any non-structured data to `stderr` - this is to make it easier to parse the structured data without having to deal with the
non-structured data.

### StdOut/StdErr usage

StdErr is a perfectly reasonable output channel. It should be the default channel for

* warnings and errors
* verbose logging
* outputs that aren't directly related to the command's primary purpose - for example 'workload manifest update' notices

There is some contention here because older versions of PowerShell treat any stderr output as an error, but this is not the
case in modern PowerShell (> 7.3) via the [$PSNativeCommandUseErrorActionPreference](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_preference_variables#psnativecommanduseerroractionpreference) preference setting.

### Advanced terminal output capabilities

You are encouraged to make use of advanced terminal capabilities like colors, weights, and other formatting options to make your output
more readable. Instead of writing entire lines of text as a single color, consider how the use of color and formatting present the
output to the user. When performing long-running operations, provide a progress bar or ticker or some other form of feedback to the user.

DO NOT rely entirely on color to convey information. Colorblind users will not be able to distinguish between different colors. Instead,
consider color, symbols, and layout holistically to convey information.

### Disabling advanced terminal output capabilities

If stderr or stdout are redirected, do not emit color/weight/progress on those channels.
If NO_COLOR is set, do not emit color/weight/progress on any channel.
If TERM is `dumb`, do not emit color/weight/progress on any channel.

## References

[Command-line interface guidelines][clig]

[clig]: https://clig.dev/
[4180]: https://www.loc.gov/preservation/digital/formats/fdd/fdd000323.shtml
[nuget-configuration]: https://nuget.org/packages/NuGet.Configuration
[pwsh-output-streams]: https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_output_streams
