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

## Grow up

When file-based programs reach an inflection point where build customizations in a project file are needed,
a single CLI command can be executed to generate a project file.
In fact, this command simply materializes the [implicit project file](#implicit-project-file) on disk.
This action should not change the behavior of the target program.

```ps1
dotnet project convert file.cs
```

The command takes a path which can be either
- path to the entry-point file in case of single entry-point programs, or
- path to the target directory (then all entry points are converted;
  it is not possible to convert just a single entry point in multi-entry-point program).

## Target path

The path passed to `dotnet run ./some/path.cs` is called *the target path*.
The target path must be a file which has the `.cs` file extension.
*The target directory* is the directory of the target file.

## Integration into the existing `dotnet run` command

`dotnet run file.cs` already has a meaning if there is a project file inside the current directory,
specifically `file.cs` is passed as the first command-line argument to the target program.
We preserve this behavior to avoid a breaking change.
The file-based build and run kicks in only when:
- a project file cannot be found (in the current directory or via the `--project` option), and
- if the target file exists and has the `.cs` file extension.

File-based programs are processed by `dotnet run` equivalently to project-based programs unless specified otherwise in this document.
For example, the remaining command-line arguments after the first argument (the target path) are passed through to the target app
(except for the arguments recognized by `dotnet run` unless they are after the `--` separator)
and working directory is not changed (e.g., `cd /x/ && dotnet run /y/file.cs` runs the program in directory `/x/`).

## Entry points

If a file is given to `dotnet run`, it has to be an *entry-point file*, otherwise an error is reported.
We want to report an error for non-entry-point files to avoid the confusion of being able to `dotnet run util.cs`.

Internally, the SDK CLI detects entry points by parsing all `.cs` files in the directory tree of the entry point file with default parsing options (in particular, no `<DefineConstants>`)
and checking which ones contain top-level statements (`Main` methods are not supported for now as that would require full semantic analysis, not just parsing).
Results of this detection are used to exclude other entry points from [builds](#multiple-entry-points) and [app directive collection](#directives-for-project-metadata).
This means the CLI might consider a file to be an entry point which later the compiler doesn't
(for example because its top-level statements are under `#if !SYMBOL` and the build has `DefineConstants=SYMBOL`).
However such inconsistencies should be rare and hence that is a better trade off than letting the compiler decide which files are entry points
because that could require multiple builds (first determine entry points and then re-build with app directives except those from other entry points).
To avoid parsing all C# files twice (in CLI and in the compiler), the CLI could use the compiler server for parsing so the trees are reused
(unless the parse options change via the directives), and also [cache](#optimizations) the results to avoid parsing on subsequent runs.

## Multiple C# files

Because of the [implicit project file](#implicit-project-file),
other files in the target directory or its subdirectories are included in the compilation.
For example, other `.cs` files but also `.resx` (embedded resources).
Similarly, implicit build files like `Directory.Build.props` or `Directory.Packages.props` are used during the build.

### Nested files

If there are nested project files like
```
App/File.cs
App/Nested/Nested.csproj
App/Nested/File.cs
```
executing `dotnet run app/file.cs` includes the nested `.cs` file in the compilation.
That is consistent with normal builds with explicit project files
and usually the build fails because there are multiple entry points or other clashes.

For `.csproj` files inside the target directory and its parent directories, we do not report any errors/warnings.
That's because it might be perfectly reasonable to have file-based programs nested in another project-based program
(most likely excluded from that project's compilation via something like `<Compile Exclude="./my-scripts/**" />`).

### Multiple entry points

If there are multiple entry-point files in the target directory, the build ignores other entry-point files.
It is an error to have an entry-point file in a subdirectory of the target directory
(because it is unclear how such program should be converted to a project-based one).

Thanks to this, it is possible to have a structure like
```
App/Util.cs
App/Program1.cs
App/Program2.cs
```
where either `Program1.cs` or `Program2.cs` can be run and both of them have access to `Util.cs`.

Behind the scenes, there are multiple implicit projects
(and during [grow up](#grow-up), multiple project files are materialized
and the original C# files are moved to the corresponding project subdirectories):
```
App/Shared/Util.cs
App/Program1/Program1.cs
App/Program1/Program1.csproj
App/Program2/Program2.cs
App/Program2/Program2.csproj
```

The generated folders might need to be named differently to avoid clashes with existing folders.

The entry-point projects (`Program1` and `Program2` in our example)
have the shared `.cs` files source-included via `<Compile Include="../Shared/**/*.cs" />`.

## Build outputs

Build outputs are placed under a subdirectory whose name is hashed file path of the entry point
inside a temp or app data directory which should be owned by and unique to the current user per [runtime guidelines][temp-guidelines].
The subdirectory is created by the SDK CLI with permissions restricting access to it to the current user (`0700`) and the run fails if that is not possible.
Note that it is possible for multiple users to run the same file-based program, however each user's run uses different build artifacts since the base directory is unique per user.
Apart from keeping the source directory clean, such artifact isolation also avoids clashes of build outputs that are not project-scoped, like `project.assets.json`, in the case of multiple entry-point files.

Artifacts are cleaned periodically by a background task that is started by `dotnet run` and
removes current user's `dotnet run` build outputs that haven't been used in some time.
They are not cleaned immediately because they can be re-used on subsequent runs for better performance.

## Directives for project metadata

It is possible to specify some project metadata via *app directives*
which are [ignored][ignored-directives] by the C# language but recognized by the SDK CLI.
Directives `sdk`, `package`, and `property` are translated into `<Project Sdk="...">`, `<PackageReference>`, and `<Property>` project elements, respectively.
Other directives result in an error, reserving them for future use.

```cs
#:sdk Microsoft.NET.Sdk.Web
#:property TargetFramework net11.0
#:property LangVersion preview
#:package System.CommandLine@2.0.0-*
```

The value must be separated from the name of the directive by white space (`@` is additionally allowed separator for the package directive)
and any leading and trailing white space is not considered part of the value.
Any value can optionally have two parts separated by a space (more whitespace characters could be allowed in the future).
The value of the first `#:sdk` is injected into `<Project Sdk="{0}">` with the separator (if any) replaced with `/`,
and the subsequent `#:sdk` directive values are split by the separator and injected as `<Sdk Name="{0}" Version="{1}" />` elements (or without the `Version` attribute if there is no separator).
It is an error if the first part (name) is empty (the version is allowed to be empty, but that results in empty `Version=""`).
The value of `#:property` is split by the separator and injected as `<{0}>{1}</{0}>` in a `<PropertyGroup>`.
It is an error if no separator appears in the value or if the first part (property name) is empty (the property value is allowed to be empty) or contains invalid characters.
The value of `#:package` is split by the separator and injected as `<PackageReference Include="{0}" Version="{1}">` (or without the `Version` attribute if there is no separator) in an `<ItemGroup>`.
It is an error if the first part (package name) is empty (the package version is allowed to be empty, but that results in empty `Version=""`).

Because these directives are limited by the C# language to only appear before the first "C# token" and any `#if`,
dotnet CLI can look for them via a regex or Roslyn lexer without any knowledge of defined conditional symbols
and can do that efficiently by stopping the search when it sees the first "C# token".

For a given `dotnet run file.cs`, we include directives from the current entry point file (`file.cs`) and all other non-entry-point files.
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

It is valid to have a `#:package` directive without a version.
That's useful when central package management (CPM) is used.
NuGet will report an appropriate error if the version is missing and CPM is not enabled.

During [grow up](#grow-up), `#:` directives are removed from the `.cs` files and turned into elements in the corresponding `.csproj` files.
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

MSBuild invocation can be skipped in subsequent `dotnet run file.cs` invocations if an up-to-date check detects that inputs didn't change.
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

An option like `dotnet run --cs-from-stdin` could read the C# file from standard input.
In this case, the current working directory would not be used to search for project or other C# files;
the compilation would consist solely of the single file read from standard input.

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
Furthermore, users might expect there to be a `--file` option, as well. Both could be unified as `--path`.

If we want to also support [multi-entry-point scenarios](#multiple-entry-points),
we might need an option like `dotnet run --entry ./dir/name` which would work for both `./dir/name.cs` and `./dir/name/name.csproj`.

### Nested files errors

Performance issues might arise if there are many [nested files](#nested-files) (possibly unintentionally),
and it might not be clear to users that `dotnet run file.cs` will include other `.cs` files in the compilation.
Therefore, we could consider some switch (a command-line option and/or a `#` language directive) to enable/disable this behavior.
When disabled, [grow up](#grow-up) would generate projects in subdirectories
similarly to [multi-entry-point scenarios](#multiple-entry-points) to preserve the program's behavior.

Including `.cs` files from nested folders which contain `.csproj`s might be unexpected,
hence we could consider reporting an error in such situations.

Similarly, we could report an error if there are many nested directories and files,
so for example, if someone puts a C# file into `C:/sources` and executes `dotnet run C:/sources/file.cs` or opens that in the IDE,
we do not walk all user's sources. Again, this problem exists with project-based programs as well.
Note that having a project-based or file-based program in the drive root would result in
[error MSB5029](https://learn.microsoft.com/visualstudio/msbuild/errors/msb5029).

### Multiple entry points implementation

We could consider using `InternalsVisibleTo` attribute but that might result in slight differences between single- and multi-entry-point programs
(if not now then perhaps in the future if [some "more internal" accessibility](https://github.com/dotnet/csharplang/issues/6794) is added to C# which doesn't respect `InternalsVisibleTo`)
which would be undesirable when users start with a single entry point and later add another.
Also, `InternalsVisibleTo` needs to be added into a C# file as an attribute, or via a complex-looking `AssemblyAttribute` item group into the `.csproj` like:

```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute" _Parameter1="App.Shared" />
</ItemGroup>
```

### Shebang support

It might be beneficial to also ship `dotnet-run` binary
(or `dotnet-run-file` that would only work with file-based programs, not project-based ones, perhaps simply named `cs`)
because some shells do not support multiple command-line arguments in the shebang
which is needed if one wants to use `/usr/bin/env` to find the `dotnet` executable
(although `-S` argument can be sometimes used to enable multiple argument support):

```cs
#!/usr/bin/env dotnet run
// ^ Might not work in all shells. "dotnet run" might be passed as a single argument to "env".
```
```cs
#!/usr/bin/env dotnet-run
// ^ Should work in all shells.
```
```cs
#!/usr/bin/env -S dotnet run
// ^ Workaround in some shells.
```

We could also consider making `dotnet file.cs` work because `dotnet file.dll` also works today
but that would require changes to the native dotnet host.

### Other commands

Commands `dotnet restore file.cs` and `dotnet build file.cs` are needed for IDE support and hence work for file-based programs.
We can consider supporting other commands like `dotnet pack`, `dotnet watch`,
however the primary scenario is `dotnet run` and we might never support additional commands.

All commands supporting file-based programs should have a way to receive the target path similarly to `dotnet run`,
e.g., via options like `--directory`/`--entry` as described [above](#integration-into-the-existing-dotnet-run-command),
or as the first argument if it makes sense for them.

We could also add `dotnet compile` command that would be the equivalent of `dotnet build` but for file-based programs
(because "compiling" might make more sense for file-based programs than "building").

`dotnet clean` could be extended to support cleaning [the output directory](#build-outputs),
e.g., via `dotnet clean --file-based-program <path-to-entry-point>`
or `dotnet clean --all-file-based-programs`.


Adding package references via `dotnet package add` could be supported for file-based programs as well,
i.e., the command would add a `#:package` directive to the top of a `.cs` file.

### Explicit importing

Instead of implicitly including files from the target directory, the importing could be explicit, like via a directive:

```cs
#import ./another-file.cs
```

<!--
## Links
-->

[artifacts-output]: https://learn.microsoft.com/dotnet/core/sdk/artifacts-output
[ignored-directives]: https://github.com/dotnet/csharplang/blob/main/proposals/ignored-directives.md
[shebang]: https://en.wikipedia.org/wiki/Shebang_%28Unix%29
[temp-guidelines]: https://github.com/dotnet/runtime/blob/d0e6ce8332a514d70b635ca4829bf863157256fe/docs/design/security/unix-tmp.md
