## dotnet-format
<img width="480" alt="dotnet-format" src="https://user-images.githubusercontent.com/9797472/61659851-6bbdc880-ac7d-11e9-95f7-d30c7de1a18a.png">

[![Nuget](https://img.shields.io/nuget/v/dotnet-format.svg)](https://www.nuget.org/packages/dotnet-format)

|Branch| Windows (Debug)| Windows (Release)| Linux (Debug) | Linux (Release) | Localization (Debug) | Localization (Release) |
|---|:--:|:--:|:--:|:--:|:--:|:--:|
[main](https://github.com/dotnet/format/tree/main)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/format/dotnet.format?branchName=main&jobName=Windows&_configuration=debug&label=build)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=347&branchName=main)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/format/dotnet.format?branchName=main&jobName=Windows&_configuration=release&label=build)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=347&branchName=main)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/format/dotnet.format?branchName=main&jobName=Linux&_configuration=debug&label=build)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=347&branchName=main)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/format/dotnet.format?branchName=main&jobName=Linux&_configuration=release&label=build)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=347&branchName=main)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/format/dotnet.format?branchName=main&jobName=Linux_Spanish&_configuration=debug&label=build)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=347&branchName=main)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/format/dotnet.format?branchName=main&jobName=Linux_Spanish&_configuration=release&label=build)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=347&branchName=main)|


`dotnet-format` is a code formatter for `dotnet` that applies style preferences to a project or solution. Preferences will be read from an `.editorconfig` file, if present, otherwise a default set of preferences will be used. At this time `dotnet-format` is able to format C# and Visual Basic projects with a subset of [supported .editorconfig options](./docs/Supported-.editorconfig-options.md).

### New in v5.1.225507

#### New Features

- Can now specify that format run against a solution filter with `dotnet format solution.slnf`
- Can now filter diagnostics with `dotnet format --fix-analyzers --diagnostics ID0001`
- Can now generate a MSBuild binary log with `dotnet format --binary-log PATH`
- Can now support analyzers which update non-code files, such as the [PublicApiAnalyzers](https://github.com/dotnet/roslyn-analyzers#microsoftcodeanalysispublicapianalyzers)

#### Breaking Changes

- Implicit restore when fixing code style or 3rd party analyzers (disable with `--no-restore`)
- Adopt csc style for warnings and errors
- Warnings and errors are now written to the standard error stream

#### Changes
- [Add DiagnosticId to the format report (1133)](https://github.com/dotnet/format/pull/1133)
- [Reenabled .NET Core 2.1 support (1021)](https://github.com/dotnet/format/pull/1021)
- [Update System.CommandLine to 2.0.0-beta1.21216.1 (1118)](https://www.github.com/dotnet/format/pull/1118)
- [Support AdditionalDocument changes (1106)](https://www.github.com/dotnet/format/pull/1106)
- [Fix typo in examples (1082)](https://www.github.com/dotnet/format/pull/1082)
- [Run CodeStyle formatter before removing unnecessary imports (1071)](https://www.github.com/dotnet/format/pull/1071)
- [Allow Solution Filter files to be passed as the workspace path (1059)](https://www.github.com/dotnet/format/pull/1059)
- [Add .pre-commit-hooks.yaml (872)](https://www.github.com/dotnet/format/pull/872)
- [Add implicit restore when running analysis. Adds `--no-restore` option. (1015)](https://www.github.com/dotnet/format/pull/1015)
- [Add separate command for binary log (1044)](https://www.github.com/dotnet/format/pull/1044)
- [Use correct flag for codestyle codefixes (1037)](https://www.github.com/dotnet/format/pull/1037)
- [Enhance whitespace issue logging with a detailed TextChange message (1017)](https://www.github.com/dotnet/format/pull/1017)
- [Log all formatter error messages in a csc-style (1016)](https://www.github.com/dotnet/format/pull/1016)
- [LogDebug each project's applied .editorconfig (1013)](https://www.github.com/dotnet/format/pull/1013)
- [Add option to filter diagnostics by id (1007)](https://www.github.com/dotnet/format/pull/1007)
- [Fix pre-commit directory (1004)](https://www.github.com/dotnet/format/pull/1004)
- [Log warnings and errors to the standard error stream (982)](https://www.github.com/dotnet/format/pull/982)
- [Only report fixable compiler diagnostics. (981)](https://www.github.com/dotnet/format/pull/981)

### .NET Core 2.1 SDK Support

The dotnet-format 5.1.x releases will continue to support users who only have .NET Core 2.1 SDK installed. Releases with bug fixes will continue until it reaches [end of support](https://dotnet.microsoft.com/platform/support/policy/dotnet-core) on August 21, 2021. Version 6.0 will require .NET Core SDK 3.1 or higher.

### How To Install

The `dotnet-format` nuget package is [published to nuget.org](https://www.nuget.org/packages/dotnet-format/).

You can install the tool using the following command.

```console
dotnet tool install -g dotnet-format
```

#### Installing Development Builds

Development builds of `dotnet-format` are being hosted on Azure Packages. You can visit the [dotnet-format Azure Packages page](https://dev.azure.com/dnceng/public/_packaging?_a=package&feed=dotnet-tools&view=versions&package=dotnet-format&protocolType=NuGet).

You can install the latest build of the tool using the following command.

```console
dotnet tool install -g dotnet-format --version 6.0.* --add-source https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json
```

### How To Use

By default `dotnet-format` will look in the current directory for a project or solution file and use that as the workspace to format. If more than one project or solution file is present in the current directory, you will need to specify the workspace to format. You can control how verbose the output will be by using the `-v` option.

```console
Usage:
  dotnet-format [options] [<workspace>]

Arguments:
  <workspace>    A path to a solution file, a project file, or a folder containing a solution or project file. If a path is not specified then the current directory is used.

Options:
  --no-restore                        Doesn't execute an implicit restore before formatting.
  --folder, -f                        Whether to treat the `<workspace>` argument as a simple folder of files.
  --fix-whitespace, -w                Run whitespace formatting. Run by default when not applying fixes.
  --fix-style, -s <severity>          Run code style analyzers and apply fixes.
  --fix-analyzers, -a <severity>      Run 3rd party analyzers and apply fixes.
  --diagnostics <diagnostic ids>      A space separated list of diagnostic ids to use as a filter when fixing code style or 3rd party analyzers.
  --include <include>                 A list of relative file or folder paths to include in formatting. All files are formatted if empty.
  --exclude <exclude>                 A list of relative file or folder paths to exclude from formatting.
  --check                             Formats files without saving changes to disk. Terminates with a non-zero exit code if any files were formatted.
  --report <report>                   Accepts a file path, which if provided, will produce a json report in the given directory.
  --binarylog <binary-log-path>       Log all project or solution load information to a binary log file.
  --verbosity, -v <verbosity>         Set the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic]
  --version                           Show version information
```

Add `format` after `dotnet` and before the command arguments that you want to run:

| Examples                                                         | Description                                                                                        |
| ---------------------------------------------------------------- |--------------------------------------------------------------------------------------------------- |
| `dotnet format`                                                  | Formats the project or solution in the current directory.                                          |
| `dotnet format <workspace>`                                      | Formats a specific project or solution.                                                            |
| `dotnet format <workspace> -f`                                   | Formats a particular folder and subfolders.                                                        |
| `dotnet format <workspace> --fix-style warn`                     | Fixes only codestyle analyzer warnings.                                                            |
| `dotnet format <workspace> --fix-style --no-restore`             | Fixes only codestyle analyzer errors without performing an implicit restore.                       |
| `dotnet format <workspace> --fix-style --diagnostics IDE0005`    | Fixes only codestyle analyzer errors for the IDE0005 diagnostic.                                   |
| `dotnet format <workspace> --fix-whitespace --fix-style`         | Formats and fixes codestyle analyzer errors.                                                       |
| `dotnet format <workspace> --fix-analyzers`                      | Fixes only 3rd party analyzer errors.                                                              |
| `dotnet format <workspace> -wsa`                                 | Formats, fixes codestyle errors, and fixes 3rd party analyzer errors.                              |
| `dotnet format -v diag`                                          | Formats with very verbose logging.                                                                 |
| `dotnet format --include Program.cs Utility\Logging.cs`         | Formats the files Program.cs and Utility\Logging.cs                                                |
| `dotnet format --check`                                          | Formats but does not save. Returns a non-zero exit code if any files would have been changed.      |
| `dotnet format --report <report-path>`                           | Formats and saves a json report file to the given directory.                                       |
| `dotnet format --include test/Utilities/*.cs --folder`           | Formats the files expanded from native shell globbing (e.g. bash). Space-separated list of files are fed to formatter in this case. Also applies to `--exclude` option. |
| `dotnet format --include 'test/Utilities/*.cs' --folder`         | With single quotes, formats the files expanded from built-in glob expansion. A single file pattern is fed to formatter, which gets expanded internally. Also applies to `--exclude` option. |
| `ls tests/Utilities/*.cs \| dotnet format --include - --folder`  | Formats the list of files redirected from pipeline via standard input. Formatter will iterate over `Console.In` to read the list of files. Also applies to `--exclude` option. |

### How To Uninstall

You can uninstall the tool using the following command.

```console
dotnet tool uninstall -g dotnet-format
```

### How To Build From Source

You can build and package the tool using the following commands. The instructions assume that you are in the root of the repository.

```console
build -pack
# The final line from the build will read something like
# Successfully created package '..\artifacts\packages\Debug\Shipping\dotnet-format.6.0.0-dev.nupkg'.
# Use the value that is in the form `6.0.0-dev` as the version in the next command.
dotnet tool install --add-source .\artifacts\packages\Debug\Shipping -g dotnet-format --version <version>
dotnet format
```

> Note: On macOS and Linux, `.\artifacts` will need be switched to `./artifacts` to accommodate for the different slash directions.
