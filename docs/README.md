# Welcome to the dotnet-format docs!

## .editorconfig options
- [Supported .editorconfig options](./Supported-.editorconfig-options.md)

## CLI options

### Specify a workspace (Required)

A workspace path is needed when running dotnet-format. By default, the current folder will be used as the workspace path. The workspace path and type of workspace determines which code files are considered for formatting.

- Solutions and Projects - By default dotnet-format will open the workspace path as a MSBuild solution or project.
- `--folder` - When the folder options is specified the workspace path will be treated as a folder of code files.

*Example:*

Format the code files used in the format solution.

```console
dotnet-format ./format.sln
```

Format the code files used in the dotnet-format project.

```console
dotnet-format ./src/dotnet-format.csproj
```

Format the code files from the  `./src` folder.

```console
dotnet-format ./src --folder
```

### Whitespace formatting

Whitespace formatting includes the core .editorconfig settings along with the placement of spaces and newlines. The whitespace formatter is run by default when not running analysis. When you want to run analysis and fix formatting issues you must specify both.

Whitespace formatting run by default.

```console
dotnet-format ./format.sln
```

Running the whitespace formatter along with code-style analysis.

```console
dotnet-format ./format.sln --fix-whitespace --fix-style
```

### Running analysis

#### CodeStyle analysis

Running codestyle analysis requires the use of a MSBuild solution or project file as the workspace. Enforces the .NET [Language conventions](https://docs.microsoft.com/en-us/visualstudio/ide/editorconfig-language-conventions?view=vs-2019) and [Naming conventions](https://docs.microsoft.com/en-us/visualstudio/ide/editorconfig-naming-conventions?view=vs-2019).

- `--fix-style <severity>` - Runs analysis and attempts to fix issues with severity equal or greater than specified. If severity is not specified then severity defaults to error.

*Example:*

Run code-style analysis against the format solution and fix errors.

```console
dotnet-format ./format.sln --fix-style
```

Run analysis against the dotnet-format project and fix warnings and errors.

```console
dotnet-format ./src/dotnet-format.csproj --fix-style warn
```

Errors when used with the `--folder` option. Analysis requires a MSBuild solution or project.

```console
dotnet-format ./src --folder --fix-style
```

#### 3rd party analysis

Running 3rd party analysis requires the use of a MSBuild solution or project file as the workspace. 3rd party analyzers are discovered from the `<PackageReferences>` specified in the workspace project files.

- `--fix-analyzers <severity>` - Runs analysis and attempts to fix issues with severity equal or greater than specified. If no severity is specified then this defaults to error.

#### Filter diagnostics to fix

Typically when running codestyle or 3rd party analysis, all diagnostics of sufficient severity are reported and fixed. The `--diagnostics` option allows you to target a particular diagnostic or set of diagnostics of sufficient severity.

- `--diagnostics <diagnostic ids>` - When used in conjunction with `--fix-style` or `--fix-analyzer`, allows you to apply targeted fixes for particular analyzers.

*Example:*

Run code-style analysis and fix unused using directive errors.

```console
dotnet-format ./format.sln --fix-style --diagnostics IDE0005
```

### Filter files to format

You can further narrow the list of files to be formatted by specifying a list of paths to include or exclude. When specifying folder paths the path must end with a directory separator. File globbing is supported.

- `--include` - A list of relative file or folder paths to include in formatting.
- `--exclude` - A list of relative file or folder paths to exclude from formatting.

*Example:*

Other repos built as part of your project can be included using git submodules. These submodules likely contain their own .editorconfig files that are set as `root = true`. This makes it difficult to validate formatting for your project as formatting mistakes in submodules are treated as errors.

The following command sets the repo folder as the workspace. It then includes the `src` and `tests` folders for formatting. The `submodule-a` folder is excluded from the formatting validation.

```console
dotnet format -f --include ./src/ ./tests/ --exclude ./src/submodule-a/ --check
```

### Logging and Reports

- `--verbosity` - Set the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic]
- `--report` - Writes a json file to the given directory. Defaults to 'format-report.json' if no filename given.

### Validate formatting

- `--check` - Formats files without saving changes to disk. Terminates with a non-zero exit code (`2`) if any files were formatted.
