# `dotnet run file.cs`

This is a proposal for extending the dotnet CLI to allow running C# source files with no need for an explicit backing project.
We call these *file-based programs* (as opposed to *project-based programs*).

```ps1
dotnet run file.cs
```

See also [IDE spec](https://github.com/dotnet/roslyn/blob/main/docs/features/file-based-programs-vscode.md).

## Motivation

File-based programs
- should be a viable alternative to using PowerShell/bash scripts in .NET repos, and
- lower the entry barrier for new customers.

## Guiding principle

The overarching guiding principle is that file-based programs have a simple and reliable [grow up](#grow-up) story to project-based programs.
Previous file-based approaches like scripting are a variant of C# and as such have no simple and reliable grow up story.

## Implicit project file

The [guiding principle](#guiding-principle) implies that we can think of file-based programs as having an implicit project file
(also known as a virtual project file because it exists only in memory unless the file-based program is converted to a project-based program).

The implicit project file is the default project that would be created by running `dotnet new console`.
This means that the behavior of `dotnet run file.cs` can change between SDK versions if the `dotnet new console` template changes.

Additionally, the implicit project file has the following customizations:

- `PublishAot` is set to `true`, see [`dotnet publish file.cs`](#other-commands) for more details.

- `PackAsTool` is set to `true`, see [`dotnet pack file.cs`](#other-commands) for more details.

- `UserSecretsId` is set to a hash of the entry point file path.

- [File-level directives](#directives-for-project-metadata) are applied.

- The following are virtual only, i.e., not preserved after [converting to a project](#grow-up):

  - `ArtifactsPath` is set to a [temp directory](#build-outputs).

  - `PublishDir` and `PackageOutputPath` are set to `./artifacts/` so the outputs of `dotnet publish` and `dotnet pack` are next to the file-based app.

  - `RuntimeHostConfigurationOption`s are set for `EntryPointFilePath` and `EntryPointFileDirectoryPath` (except for `Publish` and `Pack` targets)
    which can be accessed in the app via `AppContext`:

    ```cs
    string? filePath = AppContext.GetData("EntryPointFilePath") as string;
    string? directoryPath = AppContext.GetData("EntryPointFileDirectoryPath") as string;
    ```

  - `FileBasedProgram` property is set to `true` and can be used by SDK targets to detect file-based apps.

  - `DisableDefaultItemsInProjectFolder` property is set to `true` which results in `EnableDefaultItems=false` by default
    in case there is a project or solution in the same directory as the file-based app.
    This ensures that items from nested projects and artifacts are not included by the app.

  - `EnableDefaultCompileItems` property is set to `false` and there is a special `Compile` item for the entry-point file instead.
    See [multiple files](#multiple-files) for more details.

  - `EnableDefaultEmbeddedResourceItems` and `EnableDefaultNoneItems` properties are set to `false` if the default SDK (`Microsoft.NET.Sdk`) is being used.
    This avoids including files like `./**/*.resx` in simple file-based apps where users usually don't expect that.
    See [multiple files](#multiple-files) for more details.

## Grow up

When file-based programs reach an inflection point where build customizations in a project file are needed,
a single CLI command can be executed to generate a project file.
In fact, this command simply materializes the [implicit project file](#implicit-project-file) on disk.
This action should not change the behavior of the target program.

```ps1
dotnet project convert file.cs
```

The command takes a path to the entry-point file.

## Target path

The path passed to `dotnet run ./some/path.cs` is called *the target path*.
The target path must point to *the entry-point file* which
either has the `.cs` file extension, or whose contents start with `#!`.
*The target directory* is the directory of the target file.

## Integration into the existing `dotnet run` command

`dotnet run file.cs` already has a meaning if there is a project file inside the current directory,
specifically `file.cs` is passed as the first command-line argument to the target program.
We preserve this behavior to avoid a breaking change.
The file-based build and run kicks in only when:
- a project file cannot be found (in the current directory or via the `--project` option), and
- if the target file exists, and has the `.cs` file extension or contents that start with `#!`.

Otherwise, project-based `dotnet run` fallback is used and you might get an error like "Couldn't find a project to run."
You can explicitly use the `--file` option to avoid the fallback behavior.
However, it is discouraged to put file-based apps in the same directory as a `.csproj` file (such file-based apps might not be properly recognized by tooling).

File-based programs are processed by `dotnet run` equivalently to project-based programs unless specified otherwise in this document.
For example, the remaining command-line arguments after the first argument (the target path) are passed through to the target app
(except for the arguments recognized by `dotnet run` unless they are after the `--` separator)
and working directory is not changed (e.g., `cd /x/ && dotnet run /y/file.cs` runs the program in directory `/x/`).

If a dash (`-`) is given instead of the target path (i.e., `dotnet run -`), the C# file to be executed is read from the standard input.
In this case, the current working directory is not used to search for other files (launch profiles, other sources in case of multi-file apps);
the compilation consists solely of the single file read from the standard input.
However, the current working directory is still used as the working directory for building and executing the program.
To reference projects relative to the current working directory (instead of relative to the temporary directory the file is isolated in),
you can use something like `#:project $(MSBuildStartupDirectory)/relative/path`.

`dotnet path.cs` is a shortcut for `dotnet run --file path.cs` provided that `path.cs` is a valid [target path](#target-path) (`dotnet -` is currently not supported)
and it is not a DLL path, built-in command, or a NuGet tool (e.g., `dotnet watch` invokes the `dotnet-watch` tool
even if a valid `watch` file-based app exists in the current directory;
one can use `dotnet ./watch` to run the file-based app).

### Other commands

Commands `dotnet restore file.cs` and `dotnet build file.cs` are needed for IDE support and hence work for file-based programs.

Commands `dotnet publish file.cs` and `dotnet pack file.cs` are also supported for file-based programs.
Note that file-based apps have implicitly set `PublishAot=true`, so publishing uses Native AOT (and building reports AOT warnings).
To opt out, use `#:property PublishAot=false` directive in your `.cs` file.
Additionally, file-based apps have implicitly set `PackAsTool=true` because file-based apps are usually tools;
again, you can opt out via `#:property PackAsTool=false`.

Command `dotnet clean file.cs` can be used to clean build artifacts of the file-based program.

Commands `dotnet package add PackageName --file app.cs` and `dotnet package remove PackageName --file app.cs`
can be used to manipulate `#:package` directives in the C# files, similarly to what the commands do for project-based apps.

## Multiple files

By default, the app only includes the entry-point file (plus any implicit build files like `Directory.Build.props` are considered).
This is achieved by having property `EnableDefaultCompileItems=false` and entry-point file `Compile` item in the virtual project.
(The `Compile` item has `Exclude="@(Compile)"` to avoid duplicate `Compile` items when using e.g., `#:include *.cs` directive).

Thanks to this, it is possible to have multiple file-based apps in a single directory.

Default items like `.resx` are included in the build only if an `#:sdk` other than `Microsoft.NET.Sdk` is used (to improve performance for simple file-based apps).
Again, this is achieved by setting `EnableDefaultNoneItems=false` and `EnableDefaultEmbeddedResourceItems=false` in the virtual project.

To customize this default behavior, you can use the `#:include`/`#:exclude` [directives](#directives-for-project-metadata)
or use some custom MSBuild code like `#:property EnableDefaultCompileItems=true`.

During [conversion](#grow-up), any `Content`, `None`, `Compile`, and `EmbeddedResource` items that do not have metadata `ExcludeFromFileBasedAppConversion=true` are copied into the output directory.
The implicit `EnableDefault*Items` properties and `Compile` item are not preserved in the converted project,
because the constructed project will have the same behavior even if all default items are included thanks to the output directory being isolated
and the conversion process only copying the items that were included in the original virtual project.

## Build outputs

Build outputs are placed under a subdirectory whose name is hashed file path of the entry point
inside a temp or app data directory which should be owned by and unique to the current user per [runtime guidelines][temp-guidelines].
The subdirectory is created by the SDK CLI with permissions restricting access to it to the current user (`0700`) and the run fails if that is not possible.
Note that it is possible for multiple users to run the same file-based program, however each user's run uses different build artifacts since the base directory is unique per user.
Apart from keeping the source directory clean, such artifact isolation also avoids clashes of build outputs that are not project-scoped, like `project.assets.json`, in the case of multiple entry-point files.

Artifacts are cleaned periodically (every 2 days) by a background task that is started by `dotnet run` and
removes current user's `dotnet run` build outputs that haven't been used in 30 days.
They are not cleaned immediately because they can be re-used on subsequent runs for better performance.
The automatic cleanup can be disabled by environment variable `DOTNET_CLI_DISABLE_FILE_BASED_APP_ARTIFACTS_AUTOMATIC_CLEANUP=true`,
but other parameters of the automatic cleanup are currently not configurable.
The same cleanup can be performed manually via command `dotnet clean-file-based-app-artifacts`.

## Directives for project metadata

It is possible to specify some project metadata via *file-level directives*
which are [ignored][ignored-directives] by the C# language but recognized by the SDK CLI.

```cs
#:sdk Microsoft.NET.Sdk.Web
#:property TargetFramework=net11.0
#:property LangVersion=preview
#:package System.CommandLine@2.0.0-*
#:project ../MyLibrary
#:include ./**/*.cs
```

Each directive has a kind (e.g., `package`), a name (e.g., `System.CommandLine`), a separator (e.g., `@`), and a value (e.g., the package version).
The value is required for `#:property`, optional for `#:package`/`#:sdk`, and disallowed for `#:project`.  

The name must be separated from the kind of the directive by whitespace
and any leading and trailing white space is not considered part of the name and value.

The directives are processed as follows:

- The name and value of the first `#:sdk` is injected into `<Project Sdk="{0}/{1}">` (or just `<Project Sdk="{0}">` if it has no value),
  and the subsequent `#:sdk` directive names and values are injected as `<Sdk Name="{0}" Version="{1}" />` elements (or without the `Version` attribute if it has no value).
  It is an error if the name is empty (the version is allowed to be empty, but that results in empty `Version=""`).

- Each `#:property` is injected as `<{0}>{1}</{0}>` in a `<PropertyGroup>`.
  It is an error if property does not have a value or if its name is empty (the value is allowed to be empty) or contains invalid characters.

- Each `#:package` is injected as `<PackageReference Include="{0}" Version="{1}">` (or without the `Version` attribute if it has no value) in an `<ItemGroup>`.
  It is an error if its name is empty (the value, i.e., package version, is allowed to be empty, but that results in empty `Version=""`).

  It is valid to have a `#:package` directive without a version.
  That's useful when central package management (CPM) is used.
  NuGet will report an appropriate error if the version is missing and CPM is not enabled.

- Each `#:project` is injected as `<ProjectReference Include="{0}" />` in an `<ItemGroup>`.
  It is an error if the value is empty.
  If the path points to an existing directory, a project file is found inside that directory and its path is used instead
  (because `ProjectReference` items don't support directory paths).
  An error is reported if zero or more than one projects are found in the directory, just like `dotnet reference add` would do.

- Each `#:include` is injected as `<{1} Include="{0}" />` in an `<ItemGroup>`
  where `{0}` is the directive's value and `{1}` is determined by its extension.
  The mapping can be customized by setting the MSBuild property `FileBasedProgramsItemMapping`
  which is by default set to `.cs=Compile;.resx=EmbeddedResource;.json=None;.razor=Content`.
  (The mapping customization is currently gated under a feature flag that can be enabled by setting the MSBuild property `ExperimentalFileBasedProgramEnableItemMapping=true`.)

  It is an error if the value is empty.

  Relative paths are resolved relative to the file containing the directive.

  This directive is currently gated under a feature flag that can be enabled by setting the MSBuild property `ExperimentalFileBasedProgramEnableIncludeDirective=true`.

- Each `#:exclude` is injected similarly to `#:include` but with `Remove="{0}"` instead of `Include="{0}"`.

  This directive is currently gated under a feature flag that can be enabled by setting the MSBuild property `ExperimentalFileBasedProgramEnableExcludeDirective=true`.

- Other directive kinds result in an error, reserving them for future use.

Directive values support MSBuild variables (like `$(..)`) normally as they are translated literally and left to MSBuild engine to process.
However, in `#:project` directives, variables might not be preserved during [grow up](#grow-up),
because there is additional processing of those directives that makes it technically challenging to preserve variables in all cases
(project directive values need to be resolved to be relative to the target directory
and also to point to a project file rather than a directory).
Note that it is not expected that variables inside the path change their meaning during the conversion,
so for example `#:project ../$(LibName)` is translated to `<ProjectReference Include="../../$(LibName)/Lib.csproj" />` (i.e., the variable is preserved).
However, variables at the start can change, so for example `#:project $(ProjectDir)../Lib` is translated to `<ProjectReference Include="../../Lib/Lib.csproj" />` (i.e., the variable is expanded).
In other directives, all variables are preserved during conversion.

Because these directives are limited by the C# language to only appear before the first "C# token" and any `#if`,
dotnet CLI can look for them via a regex or Roslyn lexer without any knowledge of defined conditional symbols
and can do that efficiently by stopping the search when it sees the first "C# token".

For a given `dotnet run file.cs`, we include directives from the current entry point file (`file.cs`) and all other non-entry-point C# files,
specifically from all `Compile` items included in the project, no matter whether the `Compile` items are specified in some MSBuild code or inferred from `#:include`.
(Processing directives from other files is currently gated under a feature flag that can be enabled by setting the MSBuild property `ExperimentalFileBasedProgramEnableTransitiveDirectives=true`.)
The order in which other files are processed is currently unspecified (can change across SDK versions) but deterministic (stable in a given SDK version).
We do not limit these directives to appear only in entry point files because it allows:
- a non-entry-point file like `Util.cs` to be self-contained and have all the `#:package`s it needs specified in it,
- which also makes it possible to share it independently or symlink it to multiple script folders,
- and it's similar to `global using`s which users usually put into a single file but don't have to.

We disallow duplicate `#:` directives to allow us design some deduplication mechanism in the future.
Specifically, directives are considered duplicate if their type and name (case insensitive) are equal.
Later with deduplication, separate "self-contained" utilities could reference overlapping sets of packages
even if they end up in the same compilation.
For example, properties could be concatenated via `;`, more specific package versions could override less specific ones.

During [grow up](#grow-up), `#:` directives are removed from the `.cs` files and turned into elements in the converted `.csproj` file.
For project-based programs, `#:` directives are an error (reported by Roslyn when it's told it is in "project-based" mode).
`#!` directives are also removed during grow up, although we could consider to have an option to preserve them
(since they might still be valid after grow up, depending on which program they are actually specifying to "interpret" the file, i.e., it might not be `dotnet run` at all).

## Shebang

Along with `#:`, the language also ignores `#!` which could be then used for [shebang][shebang] support.

```cs
#!/usr/bin/dotnet run
Console.WriteLine("Hello");
```

## Implementation

The build is performed using MSBuild APIs on in-memory project files.

### Optimizations

If an up-to-date check detects that inputs didn't change in subsequent `dotnet run file.cs` invocations,
building is skipped (as if `--no-build` option has been passed).
The up-to-date check is not 100% precise (e.g., files imported through an implicit build file are not considered).
It is possible to enforce a full build using `--no-cache` flag or `dotnet build file.cs`
(for a more permanent opt-out, there is MSBuild property `FileBasedProgramCanSkipMSBuild=false`).
Environment variable [`DOTNET_CLI_CONTEXT_VERBOSE=true`][verbose-env] can be used to get more details about caching decisions made by `dotnet run file.cs`.

There are multiple optimization levels - skipping build altogether, running just the C# compiler, or running full MSBuild.
We always need to re-run MSBuild if implicit build files like `Directory.Build.props` change but
from `.cs` files, the only relevant MSBuild inputs are the `#:` directives,
hence we can first check the `.cs` file timestamps and for those that have changed, compare the sets of `#:` directives.
If only `.cs` files change, it is enough to invoke `csc.exe` (directly or via a build server)
re-using command-line arguments that the last MSBuild invocation passed to the compiler.
If no inputs change, it is enough to start the target executable without invoking the build at all.

## Alternatives and future work

This section outlines potential future enhancements and alternatives considered.

### Target path extensions

We could allow folders as the target path in the future (e.g., `dotnet run ./my-app/`).

Similarly, it could be possible to specify the whole C# source text in a command-line argument
like `dotnet run --cs-code 'Console.WriteLine("Hi")'`.

### Enhancing integration into the existing `dotnet run` command

`dotnet run path` stops working when a file-based program [grows up](#grow-up) into a project-based program.
Users could avoid that by using `cd path; dotnet run` instead.
For that to work always (before and after grow up),
`dotnet run` without a `--project` argument and without a project file in the current directory
would need to search for a file-based program in the current directory instead of failing.

We could add a universal option that works with both project-based and file-based programs,
like `dotnet run --directory ./dir/`. For inspiration, `dotnet test` also has a `--directory` option.
We already have a `--file` option. Both could be unified as `--path`.
Since there can be multiple entry points in a single directory, it would be useful to have
an option like `dotnet run --entry ./dir/name` which would work for both `./dir/name.cs` and `./dir/name/name.csproj`.

### Nested files errors

We could report an error if there are many nested directories and files,
so for example, if someone puts a C# file with `#:include **.cs` into `C:/sources`
and executes `dotnet run C:/sources/file.cs` or opens that in the IDE,
we do not walk all user's sources. Note that this problem exists with project-based programs as well.
Also, having a project-based or file-based program in the drive root would result in
[error MSB5029](https://learn.microsoft.com/visualstudio/msbuild/errors/msb5029).

### Shebang support

Some shells do not support multiple command-line arguments in the shebang
which is needed if one wants to use `/usr/bin/env` to find the `dotnet` executable
(although `-S` argument can be sometimes used to enable multiple argument support),
so `dotnet file.cs` instead of `dotnet run file.cs` should be used in shebangs:

```cs
#!/usr/bin/env dotnet run
// ^ Might not work in all shells. "dotnet run" might be passed as a single argument to "env".
```
```cs
#!/usr/bin/env dotnet
// ^ Should work in all shells.
```
```cs
#!/usr/bin/env -S dotnet run
// ^ Works in some shells.
```

### Other possible commands

We can consider supporting other commands like `dotnet watch`,
however the primary scenario is `dotnet run` and we might never support additional commands.

All commands supporting file-based programs should have a way to receive the target path similarly to `dotnet run`,
e.g., via options like `--directory`/`--entry` as described [above](#integration-into-the-existing-dotnet-run-command),
or as the first argument if it makes sense for them.

We could also add `dotnet compile` command that would be the equivalent of `dotnet build` but for file-based programs
(because "compiling" might make more sense for file-based programs than "building").

`dotnet clean` could be extended to support cleaning all file-based app outputs,
e.g., `dotnet clean --all-file-based-apps`.

More NuGet commands (like `dotnet nuget why` or `dotnet package list`) could be supported for file-based programs as well.

### Explicit importing

Instead of implicitly including files from the target directory, the importing could be explicit, like via a directive:

```cs
#import ./another-file.cs
```

<!--
## Links
-->

[artifacts-output]: https://learn.microsoft.com/dotnet/core/sdk/artifacts-output
[verbose-env]: https://learn.microsoft.com/dotnet/core/tools/dotnet-environment-variables#dotnet_cli_context_
[ignored-directives]: https://github.com/dotnet/csharplang/blob/main/proposals/ignored-directives.md
[shebang]: https://en.wikipedia.org/wiki/Shebang_%28Unix%29
[temp-guidelines]: https://github.com/dotnet/runtime/blob/d0e6ce8332a514d70b635ca4829bf863157256fe/docs/design/security/unix-tmp.md
