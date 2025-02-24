# `dotnet run file.cs`

This is a proposal for extending the dotnet CLI to allow running C# source files with no need for an explicit backing project.
We call these *file-based programs* (as opposed to *project-based programs*).

```ps1
dotnet run file.cs
```

> [!NOTE]
> This document describes the ideal final state, but the feature will be implemented in [stages](#stages).

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
dotnet project add
```

## Target path

The path passed to `dotnet run ./some/path.cs` is called *the target path*.
If it is a file, it is called *the target file*.
*The target directory* is the directory of the target file, or the target path if it is not a file.

We can consider adding an option like `dotnet run --from-stdin` which would read the C# file from the standard input.
In this case, the current working directory would not be used to search for project or other C# files,
the compilation would consist solely of the single file read from the standard input.

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
> like `dotnet run --path ./dir/`.

File-based programs are processed by `dotnet run` equivalently to project-based programs unless specified otherwise in this document.
For example, the remaining command-line arguments after the first argument (the target path) are passed through to the target app
(except for the arguments recognized by `dotnet run` unless they are after the `--` separator).

### Unsupported `dotnet run` options

These `dotnet run` options are not supported by file-based programs (they result in an error):
- `--launch-profile`
- `--no-build`

## Entry points

If a file is given to `dotnet run`, it has to be an *entry-point file*, otherwise an error is reported.
Currently, entry-point files must contain top-level statements,
but other entry-point forms like classic `Main` method can be recognized in the future.

Because of the [implicit project file](#implicit-project-file),
other files in the target directory or its subdirectories are included in the compilation.
For example, other `.cs` files but also `.resx` (embedded resources).
Similarly, implicit build files like `Directory.Build.props` are used during the build.

### Nested project files

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
so for example if someone puts a C# file into `C:/`
and executes `dotnet run C:/file.cs` or opens that in the IDE, we do not walk the whole drive.
Again, this problem exists with project-based programs as well, albeit it's likely uncommon there
since creating projects with `dotnet new` or an IDE creates them in a directory.

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
App/Shared/Shared.csproj
App/Program1/Program1.cs
App/Program1/Program1.csproj
App/Program2/Program2.cs
App/Program2/Program2.csproj
```

The generated folders might need to be named differently to avoid clashes with existing folders.

Unless the [artifacts output layout][artifacts-output] is used (which is recommended),
those implicit projects mean that build artifacts are placed under those implicit directories
even though they don't exist on disk prior to build:
```
App/Shared/bin/
App/Shared/obj/
App/Program1/bin/
App/Program1/obj/
App/Program2/bin/
App/Program2/obj/
```

## Package references

It is possible to specify NuGet package references via the `#r` directive.

```cs
#r "nuget: Newtonsoft.Json, 13.0.1"
```

The C# language needs to be updated to ignore these directives (instead of failing the compilation).
See [the corresponding language proposal][pound].

If these directives were limited by the language to only appear near the top of the file (similar to `#define` directives),
the dotnet CLI could be more efficient in searching for them.

It should be also possible to look for these directives from the dotnet CLI via a regex instead of parsing the whole C# file via Roslyn.

We can also consider reporting an error or warning if `#r` directives are spread among multiple files,
to encourage users to keep them in a single place for clarity.
Furthermore, if the directives are limited to just one file per virtual project,
it is more efficient to search for them by the dotnet CLI
(after the initial search, we can only look at a couple of files on re-runs).
For example, users could put their `#r`s into `Program1.cs`, `Program2.cs`, and `Util.cs`, but not `Util2.cs`.

## Shebang

Along with `#r`, the language can also ignore `#!` which could be then used for [shebang][shebang] support.

```cs
#!/usr/bin/dotnet run
Console.WriteLine("Hello");
```

It might be beneficial to also ship `dotnet-run` binary
(or `dotnet-run-file` that would only work with file-based programs, not project-based ones)
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

## Other commands

We can consider supporting other commands like `dotnet build`, `dotnet pack`, `dotnet watch`.

## Implementation

The build is performed using MSBuild APIs on in-memory project files.

### Optimizations

MSBuild invocation can be skipped in subsequent `dotnet run file.cs` invocations if an up-to-date check detects that inputs didn't change.
We always need to re-run MSBuild if implicit build files like `Directory.Build.props` change but
from `.cs` files, the only relevant MSBuild inputs are the `#r` directives,
hence we can first check the `.cs` file timestamps and for those that have changed, compare the sets of `#r` directives.
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
- Package references via `#r`.

<!--
## Links
-->

[artifacts-output]: https://learn.microsoft.com/dotnet/core/sdk/artifacts-output
[pound]: https://github.com/dotnet/csharplang/issues/3507
[shebang]: https://en.wikipedia.org/wiki/Shebang_%28Unix%29
