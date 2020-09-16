| Table of Contents |
|-------------------|
| [Getting Started](#getting-started) |
| [Coding Style](#coding-style) |
| [Branching Information](#branching-information) |

# Getting Started #
To contribute, please fork the repo and develop in a feature branch off the current [development branch](#branching-information). When you've prepared the change you wish to make, please rebase against the development branch before submitting your PR.

## Prerequisites ##

### For Windows ###

1. git (available from http://www.git-scm.com/) on the PATH.

### For Linux ###

1. git (available from http://www.git-scm.com/) on the PATH.

## Building ##

### For Windows ###
1. Run `build.cmd`

### For non-Windows ###
1. Run `build.sh`

### Changing build configurations ###
To build in `DEBUG` mode - set the environment variable `DN3B` to `DEBUG`
To build in `RELEASE` mode (default) - set the environment variable `DN3B` to `RELEASE`

## Running ##
1. Run `dotnet new3` at the command line

### Note for Windows users ###
The location `dotnet-new3.exe` gets built to will be placed at the start of the `PATH` environment variable, so it won't become available in console windows (other than the one you've built in) that are already open. To run in already open windows, you can add the `dev` directory (created during the build) to the `PATH` environment variable, or run `dotnet-new3.exe` from that directory.

### Note for non-Windows users ###
`setup.sh` attempts to create a symlink to the `dotnet-new3` executable in `/usr/local/bin/`, if the attempt to elevate to do that is denied, you can still run the `dotnet-new3` executable directly in the `dev` directory created during the build.

[Top](#top)

# Coding Style #

* All block statements _must_ use braces (if/else, for, foreach, using, etc.)
  * Immediately nested usings do not need braces at the outer layers
* We use four spaces of indentation (no tabs).
* We use explicit types (no usages of `var`)
* We always specify the visibility, even if it's the default (i.e. private string _foo not string _foo). Visibility should be the first modifier (i.e. public abstract not abstract public).
* We avoid this. unless absolutely necessary.
* We use `_camelCase` for private fields and use readonly where possible. When used on static fields, readonly should come after static (i.e. static readonly not readonly static).
* Namespace imports should be specified at the top of the file, outside of namespace declarations and should be sorted alphabetically with the exception of `System.*` namespaces, which are placed first.
* We use language keywords instead of BCL types (i.e. `int`, `string`, `float` instead of `Int32`, `String`, `Single`, etc) for both type references as well as method calls (i.e. `int.Parse` instead of `Int32.Parse`).
* We use PascalCasing to name all our methods, properties, constant local variables, and static readonly fields.
* We use `nameof(...)` instead of `"..."` whenever possible and relevant.
* Fields should be specified at the top within type declarations.

[Top](#top)

# Branching information #

| Topic | Branch |
|-------|-------|
| Development | *stabilize* |
| Next | *master* |
| Current Release | *release/2.1* |

[Top](#top)
