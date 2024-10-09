# Welcome to the dotnet-format docs!

## .editorconfig options
- [Supported .editorconfig options](./Supported-.editorconfig-options.md)

## CLI options

### Specify a workspace (Required)

A workspace path is needed when running dotnet-format. By default, the current folder will be used as the workspace path. The workspace path and type of workspace determines which code files are considered for formatting.

- Solutions and Projects - By default dotnet-format will open the workspace path as a MSBuild solution or project.
- `--no-restore` - When formatting a solution or project the no restore option will stop dotnet-format from performing an implicit package restore.
- `--folder` - When the folder options is specified the workspace path will be treated as a folder of code files.

*Example:*

Format the code files used in the format solution.

```console
dotnet format ./format.sln
```

Format the code files used in the dotnet-format project.

```console
dotnet format ./src/dotnet-format.csproj
```

Format the code files from the  `./src` folder.

```console
dotnet format whitespace ./src --folder
```

### Whitespace formatting

Whitespace formatting includes the core .editorconfig settings along with the placement of spaces and newlines. The whitespace formatter is run by default when not running analysis. When only performing whitespace formatting, an implicit restore is not perfomed. When you want to run analysis and fix formatting issues you must specify both.

Whitespace formatting run by default along with code-style and 3rd party analysis.

```console
dotnet format ./format.sln
```

Running the whitespace formatter alone.

```console
dotnet format whitespace ./format.sln
```

### Running analysis

#### CodeStyle analysis

Running codestyle analysis requires the use of a MSBuild solution or project file as the workspace. By default an implicit restore on the solution or project is performed. Enforces the .NET [Language conventions](https://docs.microsoft.com/en-us/visualstudio/ide/editorconfig-language-conventions?view=vs-2019) and [Naming conventions](https://docs.microsoft.com/en-us/visualstudio/ide/editorconfig-naming-conventions?view=vs-2019).

- `dotnet format style --severity <severity>` - Runs analysis and attempts to fix issues with severity equal or greater than specified. If severity is not specified then severity defaults to warning.

*Example:*

Code-style analysis is run by default along with whitespace formatting and 3rd party analysis.

```console
dotnet format ./format.sln
```

Run code-style analysis alone against the dotnet-format project.

```console
dotnet format style ./src/dotnet-format.csproj --severity error
```

Errors when used with the `--folder` option. Analysis requires a MSBuild solution or project.

```console
dotnet format style ./src --folder
```

#### 3rd party analysis

Running 3rd party analysis requires the use of a MSBuild solution or project file as the workspace. By default an implicit restore on the solution or project is performed. 3rd party analyzers are discovered from the `<PackageReferences>` specified in the workspace project files.

- `dotnet format analyzers --severity <severity>` - Runs analysis and attempts to fix issues with severity equal or greater than specified. If no severity is specified then this defaults to warning.

#### Filter diagnostics to fix

Typically when running codestyle or 3rd party analysis, all diagnostics of sufficient severity are reported and fixed. The `--diagnostics` option allows you to target a particular diagnostic or set of diagnostics of sufficient severity.

- `--diagnostics <diagnostic ids>` - When used in conjunction with `style` or `analyzer` subcommands, allows you to apply targeted fixes for particular analyzers.

*Example:*

Run code-style analysis and fix unused using directive errors.

```console
dotnet format style ./format.sln --diagnostics IDE0005
```

### Filter files to format

You can further narrow the list of files to be formatted by specifying a list of paths to include or exclude. When specifying folder paths the path must end with a directory separator. File globbing is supported.

- `--include` - A list of relative file or folder paths to include in formatting.
- `--exclude` - A list of relative file or folder paths to exclude from formatting.

*Example:*

Other repos built as part of your project can be included using git submodules. These submodules likely contain their own .editorconfig files that are set as `root = true`. This makes it difficult to validate formatting for your project as formatting mistakes in submodules are treated as errors.

The following command sets the repo folder as the workspace. It then includes the `src` and `tests` folders for formatting. The `submodule-a` folder is excluded from the formatting validation.

```console
dotnet format whitespace --folder --include ./src/ ./tests/ --exclude ./src/submodule-a/ --verify-no-changes
```

### Logging and Reports

- `--verbosity` - Set the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic]
- `--report` - Writes a json file to the given directory. Defaults to 'format-report.json' if no filename given.
- `--binarylog` - Writes a [binary log file](https://msbuildlog.com/) to help in diagnosing solution or project load errors. Defaults to 'format.binlog' if no filename given.

### Validate formatting

- `--verify-no-changes` - Formats files without saving changes to disk. Terminates with a non-zero exit code (`2`) if any files were formatted.
