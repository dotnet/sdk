# `dotnet run file.cs`

This is a proposal for extending the dotnet CLI to allow running C# source files with no need for an explicit backing project.
We call these *file-based programs* (as opposed to *project-based programs*).

```ps1
dotnet run file.cs
```

> [!NOTE]
> This document describes the ideal final state, but the feature will be implemented in [stages](#stages).

> [!CAUTION]
> The current implementation has been limited to single file support for the initial preview
> (as if the implicit project file had `<EnableDefaultItems>false</EnableDefaultItems>` and an explicit `<Compile Include="file.cs" />`),
> but this proposal describes a situation where all files in the target directory are included.
> Once a final decision is made, the proposal will be updated.

## Motivation

File-based programs
- should be a viable alternative to using PowerShell/bash scripts in .NET repos, and
- lower the entry barrier for new customers.

## Guiding principle

The overarching guiding principle is that file-based programs have a simple and reliable [grow up](#grow-up) story to project-based programs.
Previous file-based approaches like scripting are a variant of C# and as such have no simple and reliable grow up story.

## Implicit project file

The [guiding principle](#guiding-principle) implies that we can think of file-based programs as having an implicit project file.

The implicit project file is the default project that would be created by running `dotnet new console`.
This means that the behavior of `dotnet run file.cs` can change between SDK versions if the `dotnet new console` template changes.
In the future we can consider supporting more SDKs like the Web SDK.

## Grow up

When file-based programs reach an inflection point where build customizations in a project file are needed,
a single CLI command can be executed to generate a project file.
In fact, this command simply materializes the [implicit project file](#implicit-project-file) on disk.
This action should not change the behavior of the target program.

```ps1
dotnet project convert
```

## Target path

The path passed to `dotnet run ./some/path.cs` is called *the target path*.
If it is a file, it is called *the target file*.
*The target directory* is the directory of the target file, or the target path if it is not a file.

We can consider adding an option like `dotnet run --from-stdin` which would read the C# file from the standard input.
In this case, the current working directory would not be used to search for project or other C# files,
the compilation would consist solely of the single file read from the standard input.
Similarly, it could be possible to specify the whole C# source text in a command-like argument
like `dotnet run --code 'Console.WriteLine("Hi")'`.

## Integration into the existing `dotnet run` command

`dotnet run file.cs` already has a meaning if there is a project file inside the current directory,
specifically `file.cs` is passed as the first command-line argument to the target program.
We preserve this behavior to avoid a breaking change.
The file-based build and run kicks in only when:
- a project file cannot be found (in the current directory or via the `--project` option), and
- if the target path is a file, it has the `.cs` file extension, and
- the target path (file or directory) exists.

> [!NOTE]
> This means that `dotnet run path` stops working when a file-based program [grows up](#grow-up) into a project-based program.
>
> Users could avoid that by using `cd path; dotnet run` instead. For that to work always (before and after grow up),
> `dotnet run` without a `--project` argument and without a project file in the current directory
> would need to search for a file-based program in the current directory instead of failing.
>
> We can also consider adding some universal option that would work with both project-based and file-based programs,
> like `dotnet run --directory ./dir/`. For inspiration, `dotnet test` also has a `--directory` option.
> Although users might expect there to be a `--file` option, as well. Both could be unified as `--path`.
>
> If we want to also support [multi-entry-point scenarios](#multiple-entry-points),
> we might need an option like `dotnet run --entry ./dir/name`
> which would work for both `./dir/name.cs` and `./dir/name/name.csproj`.

File-based programs are processed by `dotnet run` equivalently to project-based programs unless specified otherwise in this document.
For example, the remaining command-line arguments after the first argument (the target path) are passed through to the target app
(except for the arguments recognized by `dotnet run` unless they are after the `--` separator).

## Entry points

If a file is given to `dotnet run`, it has to be an *entry-point file*, otherwise an error is reported.
We want to report an error for non-entry-point files to avoid the confusion of being able to `dotnet run util.cs`.

We modify Roslyn to accept the entry-point path and then it is its responsibility
to check whether the file contains an entry point (top-level statements or a valid Main method) and report an error otherwise.
(We cannot simply use Roslyn APIs to detect entry points ourselves because parsing depends on conditional symbols like those from `<DefineConstants>`
and we can reliably know the set of those only after invoking MSBuild, and doing that up front would be an unnecessary performance hit just to detect entry points.)

Because of the [implicit project file](#implicit-project-file),
other files in the target directory or its subdirectories are included in the compilation.
For example, other `.cs` files but also `.resx` (embedded resources).
Similarly, implicit build files like `Directory.Build.props` or `Directory.Packages.props` are used during the build.

> [!NOTE]
> Performance issues might arise if there are many [nested files](#nested-files) (possibly unintentionally),
> and also it might not be clear to users that `dotnet run file.cs` will include other `.cs` files in the compilation.
> Therefore we could consider some switch (a command-line option and/or a `#` language directive) to enable/disable this behavior.
> When disabled, [grow up](#grow-up) would generate projects in subdirectories similarly to [multi-entry-point scenarios](#multiple-entry-points)
> to preserve the behavior.

### Nested files

If there are nested project files like
```
App/File.cs
App/Nested/Nested.csproj
App/Nested/File.cs
```
executing `dotnet run app/file.cs` includes the nested `.cs` file in the compilation.
That might be unexpected, hence we could consider reporting an error in such situation.
However, the same problem exists for normal builds with explicit project files
and usually the build fails because there are multiple entry points or other clashes.

Similarly, we could report an error if there are many nested directories and files,
so for example if someone puts a C# file into `C:/sources`
and executes `dotnet run C:/sources/file.cs` or opens that in the IDE, we do not walk all user's sources.
Again, this problem exists with project-based programs as well.
Note that having a project-based or file-based program in the drive root would result in
[error MSB5029](https://learn.microsoft.com/visualstudio/msbuild/errors/msb5029).

For `.csproj` files inside the target directory and its parent directories, we do not report any errors/warnings.
That's because it might be perfectly reasonable to have file-based programs nested in another project-based program
(most likely excluded from that project's compilation via something like `<Compile Exclude="./my-scripts/**" />`).

### Multiple entry points

If there are multiple entry-point files in the target directory, the target path must be a file
(an error is reported if it points to a directory instead).
Then the build ignores other entry-point files.

Thanks to this, it is possible to have a structure like
```
App/Util.cs
App/Program1.cs
App/Program2.cs
```
where either `Program1.cs` or `Program2.cs` can be run and both of them have access to `Util.cs`.

In this case, there are multiple implicit projects
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
have the shared `.cs` files source-included via `<Content Include="../Shared/**/*.cs" />`.
We could consider having the projects directly in the top-level folder instead
but that might result in clashes of build outputs that are not project-scoped, like `project.assets.json`.
If we did that though, it would be enough to exclude the other entry points rather than including all the shared `.cs` files.

Unless the [artifacts output layout][artifacts-output] is used (which is recommended),
those implicit projects mean that build artifacts are placed under those implicit directories
even though they don't exist on disk prior to build:
```
App/Program1/bin/
App/Program1/obj/
App/Program2/bin/
App/Program2/obj/
```

## Directives for project metadata

It is possible to specify some project metadata via [ignored C# directives][ignored-directives].
Directives `sdk`, `package`, and `property` are translated into `<Project Sdk="...">`, `<PackageReference>`, and `<Property>` project elements, respectively.
Other directives result in a warning, reserving them for future use.

```cs
#:sdk Microsoft.NET.Sdk.Web
#:property TargetFramework net11.0
#:property LangVersion preview
#:package System.CommandLine 2.0.0-*
```

The value must be separated from the name of the directive by white space and any leading and trailing white space is not considered part of the value.
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

We do not limit these directives to appear only in entry point files.
Indeed, it might be beneficial to let a non-entry-point file like `Util.cs` be self-contained and have all the `#:package`s it needs specified in it,
which also makes it possible to share it independently or symlink it to multiple script folders.
This is also similar to `global using`s which users usually put into a single file but don't have to.

We could consider deduplicating `#:` directives
(e.g., properties could be concatenated via `;`, more specific package versions could override less specific ones),
so for example separate "self-contained" utilities could reference overlapping sets of packages
even if they end up in the same compilation.
But for starters we can translate each directive into the corresponding project element
and let the existing MSBuild/NuGet logic deal with duplicates.

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

## Other commands

We can consider supporting other commands like `dotnet build`, `dotnet pack`, `dotnet watch`.

These commands need to have a way to receive the target path similarly to `dotnet run`,
e.g., via options like `--directory`/`--entry` as described [above](#integration-into-the-existing-dotnet-run-command),
or as the first argument if it makes sense for them.

We could also add `dotnet compile` command that would be the equivalent of `dotnet build` but for file-based programs
(because "compiling" might make more sense for file-based programs than "building").

### `dotnet package add`

Adding package references via `dotnet package add` could be supported for file-based programs as well,
i.e., the command would add a `#:package` directive to the top of a `.cs` file.

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

### Stages

The plan is to implement the feature in stages (the order might be different):

- Bare bones `dotnet run file.cs` support: only files, not folders; a single entry-point; no optimizations.
- Optimizations (caching / up-to-date check).
- Multiple entry points.
- Grow up command.
- Folder support: `dotnet run ./dir/`.
- Project metadata via `#:` directives.

## Alternatives

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
