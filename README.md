## dotnet-format
<img width="480" alt="dotnet-format" src="https://user-images.githubusercontent.com/9797472/61659851-6bbdc880-ac7d-11e9-95f7-d30c7de1a18a.png">

[![Nuget](https://img.shields.io/nuget/v/dotnet-format.svg)](https://www.nuget.org/packages/dotnet-format)

[![MyGet](https://img.shields.io/dotnet.myget/format/vpre/dotnet-format.svg?label=myget)](https://dotnet.myget.org/feed/format/package/nuget/dotnet-format)

|Branch| Windows (Debug)| Windows (Release)| Linux (Debug) | Linux (Release) | Localization (Debug) | Localization (Release) |
|---|:--:|:--:|:--:|:--:|:--:|:--:|
[master](https://github.com/dotnet/format/tree/master)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/format/dotnet.format?branchName=master&jobName=Windows&configuration=debug&label=build)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=347&branchName=master)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/format/dotnet.format?branchName=master&jobName=Windows&configuration=release&label=build)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=347&branchName=master)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/format/dotnet.format?branchName=master&jobName=Linux&configuration=debug&label=build)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=347&branchName=master)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/format/dotnet.format?branchName=master&jobName=Linux&configuration=release&label=build)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=347&branchName=master)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/format/dotnet.format?branchName=master&jobName=Windows_Spanish&configuration=debug&label=build)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=347&branchName=master)|[![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/format/dotnet.format?branchName=master&jobName=Windows_Spanish&configuration=release&label=build)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=347&branchName=master)|


`dotnet-format` is a code formatter for `dotnet` that applies style preferences to a project or solution. Preferences will be read from an `.editorconfig` file, if present, otherwise a default set of preferences will be used. At this time `dotnet-format` is able to format C# and Visual Basic projects with a subset of [supported .editorconfig options](https://github.com/dotnet/format/wiki/Supported-.editorconfig-options).

### New in v3.3.111304
- [Enhance --files option to support folder paths and add --include alias (533)](https://github.com/dotnet/format/pull/533)
- [format-500: Add `--exclude` option to ignore given files/folders (529)](https://github.com/dotnet/format/pull/529)
- [format-379: Add `--report` command line argument to export json format report to given directory (495)](https://github.com/dotnet/format/pull/495)
- [Update charset formatter to check for equivalent encodings (508)](https://github.com/dotnet/format/pull/508)

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
dotnet tool install -g dotnet-format --version 3.0.5-prerelease.19203.5 --add-source https://dotnet.myget.org/F/format/api/v3/index.json
```

### How To Use

By default `dotnet-format` will look in the current directory for a project or solution file and use that as the workspace to format. If more than one project or solution file is present in the current directory you will need to specify the workspace to format using the `-w` option. You can control how verbose the output will be by using the `-v` option.

```
Usage:
  dotnet-format [options]

Options:
  --folder, -f       The folder to operate on. Cannot be used with the `--workspace` option.
  --workspace, -w    The solution or project file to operate on. If a file is not specified, the command will search
                     the current directory for one.
  --verbosity, -v    Set the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and
                     diag[nostic]
  --dry-run          Format files, but do not save changes to disk.
  --check            Terminates with a non-zero exit code if any files were formatted.
  --include, --files A comma separated list of relative file or folder paths to include in formatting. All files are
                     formatted if empty.
  --exclude          A comma separated list of relative file or folder paths to exclude from formatting.
  --version          Display version information
  --report           Writes a json file to the given directory. Defaults to 'format-report.json' if no filename given.
```

Add `format` after `dotnet` and before the command arguments that you want to run:

| Examples                                                   | Description                                                                                   |
| ---------------------------------------------------------- |---------------------------------------------------------------------------------------------- |
| dotnet **format**                                          | Formats the project or solution in the current directory.                                     |
| dotnet **format** -f &lt;folder&gt;                        | Formats a particular folder and subfolders.                                                   |
| dotnet **format** -w &lt;workspace&gt;                     | Formats a specific project or solution.                                                       |
| dotnet **format** -v diag                                  | Formats with very verbose logging.                                                            |
| dotnet **format** --include Programs.cs,Utility\Logging.cs | Formats the files Program.cs and Utility\Logging.cs                                           |
| dotnet **format** --check --dry-run                        | Formats but does not save. Returns a non-zero exit code if any files would have been changed. |
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
# Successfully created package '..\artifacts\packages\Debug\Shipping\dotnet-format.3.0.0-dev.nupkg'.
# Use the value that is in the form `3.2.0-dev` as the version in the next command.
dotnet tool install --add-source .\artifacts\packages\Debug\Shipping -g dotnet-format --version <version>
dotnet format
```

> Note: On macOS and Linux, `.\artifacts` will need be switched to `./artifacts` to accommodate for the different slash directions.
