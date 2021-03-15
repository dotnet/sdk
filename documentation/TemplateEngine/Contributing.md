| Table of Contents |
|-------------------|
| [Getting Started](#getting-started) |
| [Build & Run](#build--run) |
| [Debugging](#debugging) |
| [Coding Style](#coding-style) |
| [Branching](#branching) |

# Getting Started #

If you're authoring templates, or interested in contributing to this repo, then you're likely interested in how to use the latest version of this experience.
The steps required are outlined below.

## Prerequisites ##

1. "git" (http://www.git-scm.com/) should be installed and added to PATH.

## Acquire

- Fork this repository.
- Clone the forked repository to your local machine.
- Checkout *main* and branch off to your feature branch (see: [branching](#branching))
- Rebase against *main* before submitting your PR.

# Build & Run

- Open up a command prompt and navigate to the root of your local repo.
- Run the build script appropriate for your environment.
     - **Windows:** [build.cmd](https://github.com/dotnet/templating/blob/main/build.cmd)
     - **Mac/Linux**: [build.sh](https://github.com/dotnet/templating/blob/main/build.sh) 
- The changes you build will not change how `dotnet new` behaves. Instead, they will be available through `dotnet new3`. To run `dotnet new3`, run 
  ```
  dotnet <your repo location>\artifacts\bin\dotnet-new3\<configuration>\<target framework>\dotnet-new3.dll
  ```
  Root path to `dotnet-new3.dll` is skipped in all the commands below.

For example, here is the result of running `dotnet .\dotnet-new3.dll --help` (_truncated to save space here_).

```bash
$ dotnet .\dotnet-new3.dll --help
Usage: new3 [options]

Options:
  -h, --help          Displays help for this command.
  -l, --list          Lists templates containing the specified template name. If no name is specified, lists all templates.
  -n, --name          The name for the output being created. If no name is specified, the name of the output directory is used.
...
```
After the first run, there are no templates installed. See [Installing templates](../README.md#Installing-templates) on how to install the templates and [Available templates](../README.md#Available-templates) for the list of available templates.

This repository features "Class Library" and "Console App" templates. After you build and run, the package containing these two templates will be available in the following location:
  ```
  <local_repo_root>\artifacts\packages\Debug\Shipping\Microsoft.DotNet.Common.ProjectTemplates.*.nupkg
  ```

[Top](#top)

# Debugging
Debugging code requires your current `dotnet new3` session to have its active build session configured to DEBUG, and a debugger from your application of choice to be attached to the currently running `dotnet new3` process. The steps required to accomplish this are outlined below.

    When working with the source inside Visual Studio, it is recommended you use the latest available version.

- Open the **Microsoft.TemplatingEngine.sln** solution in the application you will use to attach your debugger. This solution contains the projects needed to run, modify & debug the Template Engine.

- Run the following command:
  ```bash
  dotnet .\dotnet-new3.dll --debug:attach {{additonal args}}
  ```
  By supplying the `--debug:attach` argument, you are triggering a ` Console.  ReadLine();` request which pauses the execution of the Template Engine at an early point in its execution.
  
  Once the engine is "paused", you have the opportunity to attach a debugger to the running `dotnet new3` process. 

- Switch to your debugger application.

- Open the **Microsoft.TemplateEngine.Cli.New3Command** class and locate the following function.
  - `New3Command.Run()`
- Set a breakpoint at any point after the following block of code.

```csharp
if (args.Any(x => string.Equals(x, "--debug:attach", StringComparison.Ordinal)))
{
    // This is the line that is executed when --debug:attach is passed as 
    // an argument. 
    Console.ReadLine();
}
```
- Attach the debugger to the current running 'dotnet new 3' process.
  For example, if you are using **Visual Studio** you can perform the following.
  - Execute the keyboard shortcut - `ctrl + alt + p`.
  - This will open up a dialog that allows you to search for the **dotnet-new3.exe** process.
  - Locate the desired process, select it and hit the **Attach** button.
    
- Now that you have a debug session attached to your properly configured `dotnet new3` process, head back to the command line and hit `enter`.  This will trigger `Console.Readline()` to execute and your proceeding breakpoint to be hit inside the application you are using to debug. 

[Top](#top)

# Coding Style #

Most of the styling is enforced by analyzers and the rules covered by the analyzers are not listed in this section. Therefore, it is highly recommended to use an IDE with Roslyn analyzers support (such as Visual Studio or Visual Studio Code).

* We only use var when the variable type is obvious.
* We avoid this, unless absolutely necessary.
* We use `_camelCase` for private fields.
* Use readonly where possible.
* We use PascalCasing to name all our methods, properties, constant local variables, and static readonly fields.
* We use `nameof(...)` instead of `"..."` whenever possible and relevant.
* We use [nullable reference types](https://docs.microsoft.com/en-us/dotnet/csharp/nullable-references) to make conscious decisions on the nullability of references, be more clear with our intent and reduce `NullReferenceException`s. Add `#nullable enabled` to the top of all the modified files unless:
  * Nullable reference types are already enabled for the file
  * The file is in one of the test projects
  * The changes you are introducing to the file are negligable in size compared to the size of the whole file,
  * You don't have enough context on the code to make decisions on nullability of types.

Some of the analyzer rules are currently being treated as "info/suggestion"s instead of "warning"s, because we have not yet done a solution wide refactoring to comply with the rules. Although it would be most welcome, you are not required to fix any of the existing suggestions. However, any code that you introduce should be free of suggestions.

[Top](#top)

# Branching #

We do development in *main* branch. After a release branch is created, any new changes that should be included in that release are cherry-picked from *main*.

We follow the same versioning as https://github.com/dotnet/sdk and release branches are named after the version numbers. For instance, `release/5.0.2xx` branch ships with .NET SDK 5.0.2xx.

| Topic | Branch |
|-------|-------|
| Development | *main* |
| Release | *release/** |

[Top](#top)
