## dotnet-format
<img width="480" alt="dotnet-format" src="https://user-images.githubusercontent.com/9797472/61659851-6bbdc880-ac7d-11e9-95f7-d30c7de1a18a.png">

[![Nuget](https://img.shields.io/nuget/v/dotnet-format.svg)](https://www.nuget.org/packages/dotnet-format)

[![MyGet](https://img.shields.io/dotnet.myget/format/vpre/dotnet-format.svg?label=myget)](https://dotnet.myget.org/feed/format/package/nuget/dotnet-format)

|Branch| Windows (Debug)| Windows (Release)| Linux (Debug) | Linux (Release) | Localization (Debug) | Localization (Release) |
|---|:--:|:--:|:--:|:--:|:--:|:--:|
[master](https://github.com/dotnet/format/tree/master)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/format/dotnet.format?branchName=master&jobName=Windows&configuration=debug&label=build)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=347&branchName=master)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/format/dotnet.format?branchName=master&jobName=Windows&configuration=release&label=build)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=347&branchName=master)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/format/dotnet.format?branchName=master&jobName=Linux&configuration=debug&label=build)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=347&branchName=master)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/format/dotnet.format?branchName=master&jobName=Linux&configuration=release&label=build)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=347&branchName=master)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/format/dotnet.format?branchName=master&jobName=Windows_Spanish&configuration=debug&label=build)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=347&branchName=master)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/format/dotnet.format?branchName=master&jobName=Windows_Spanish&configuration=release&label=build)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=347&branchName=master)|


`dotnet-format` is a code formatter for `dotnet` that applies style preferences to a project or solution. Preferences will be read from an `.editorconfig` file, if present, otherwise a default set of preferences will be used. At this time `dotnet-format` is able to format C# and Visual Basic projects with a subset of [supported .editorconfig options](./docs/Supported-.editorconfig-options.md).

### New in v4.0.130103
#### Breaking Changes:
- Added an imports formatter for sorting imports.
- Format now runs on the latest installed Runtime.
- `--check` and `--dry-run` have combined into a single option.
- `--include` and `--exclude` use space-separated paths instead of comma-separated.

#### Deprecations:
- Added warning to use the default argument instead of `--workspace` option. Use `dotnet format ./format.sln` instead of `dotnet format -w ./format.sln`
- Added warning to use the default argument to specify the folder path when using the `--folder` option. Use `dotnet format ./src -f` instead of `dotnet format -f ./src`
- Added warning to use `--include` instead of `--files` alias.
- Added warning to use `--check` instead of `--dry-run` alias.

#### Changes:
- [Add Imports Formatter (693)](https://www.github.com/dotnet/roslyn/pull/693)
- [Always run on the latest Runtime (694)](https://www.github.com/dotnet/roslyn/pull/694)
- [Move to Roslyn's editorconfig support (590)](https://www.github.com/dotnet/roslyn/pull/590)
- [Command line argument for solution/project as positional argument (681)](https://www.github.com/dotnet/roslyn/pull/681)
- [Add option to format generated code files. (673)](https://www.github.com/dotnet/roslyn/pull/673)
- [Produce a binlog when verbosity is set to detailed (605)](https://www.github.com/dotnet/roslyn/pull/605)
- [Fix #581 - Add final newline false positive (633)](https://www.github.com/dotnet/roslyn/pull/633)
- [Combine --check and --dry-run into a single option. (541)](https://github.com/dotnet/format/pull/541)
- [Use space-separated paths instead of comma-separated for --include and --exclude (551)](https://github.com/dotnet/format/pull/551)
- [Support loading commandline options from response files (552)](https://github.com/dotnet/format/pull/552)
- [Support file globbing in --include and --exclude options (555)](https://github.com/dotnet/format/pull/555)


### How To Install

The `dotnet-format` nuget package is [published to nuget.org](https://www.nuget.org/packages/dotnet-format/).

You can install the tool using the following command.

```console
dotnet tool install -g dotnet-format
```

#### Installing Development Builds

Development builds of `dotnet-format` are being hosted on myget. You can visit the [dotnet-format myget page](https://dotnet.myget.org/feed/format/package/nuget/dotnet-format) to get the latest version number.

You can install the tool using the following command.

```console
dotnet tool install -g dotnet-format --version 4.0.111308 --add-source https://dotnet.myget.org/F/format/api/v3/index.json
```

### How To Use

By default `dotnet-format` will look in the current directory for a project or solution file and use that as the workspace to format. If more than one project or solution file is present in the current directory you will need to specify the workspace to format using the `-w` or `-f` options. You can control how verbose the output will be by using the `-v` option.

```sh
Usage:
  dotnet-format <project> [options]

Options:
  --folder, -f                    Whether to treat the `<project>` path as a folder of files.
  --include <INCLUDE>             A list of relative file or folder paths to include in formatting. All files are formatted if empty.
  --exclude <EXCLUDE>             A list of relative file or folder paths to exclude from formatting.
  --check <CHECK>                 Formats files without saving changes to disk. Terminates with a non-zero exit code if any files were formatted.
  --report <REPORT>               Accepts a file path, which if provided, will produce a json report in the given directory.
  --verbosity, -v <VERBOSITY>     Set the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic]
  --version                       Display version information
```

Add `format` after `dotnet` and before the command arguments that you want to run:

| Examples                                                   | Description                                                                                   |
| ---------------------------------------------------------- |---------------------------------------------------------------------------------------------- |
| dotnet **format**                                          | Formats the project or solution in the current directory.                                     |
| dotnet **format** &lt;workspace&gt;                        | Formats a specific project or solution.                                                       |
| dotnet **format** &lt;folder&gt; -f                        | Formats a particular folder and subfolders.                                                   |
| dotnet **format** -v diag                                  | Formats with very verbose logging.                                                            |
| dotnet **format** --include Programs.cs Utility\Logging.cs | Formats the files Program.cs and Utility\Logging.cs                                           |
| dotnet **format** --check                                  | Formats but does not save. Returns a non-zero exit code if any files would have been changed. |
| dotnet **format** --report &lt;report-path&gt;             | Formats and saves a json report file to the given directory.                                  |

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
# Successfully created package '..\artifacts\packages\Debug\Shipping\dotnet-format.4.0.0-dev.nupkg'.
# Use the value that is in the form `4.0.0-dev` as the version in the next command.
dotnet tool install --add-source .\artifacts\packages\Debug\Shipping -g dotnet-format --version <version>
dotnet format
```

> Note: On macOS and Linux, `.\artifacts` will need be switched to `./artifacts` to accommodate for the different slash directions.
