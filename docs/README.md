# Welcome to the dotnet-format docs!

## .editorconfig options
- [Supported .editorconfig options](./Supported-.editorconfig-options.md)
- [Unsupported Code Style options](./Unsupported-Code-Style-options.md)

## CLI options

### Specify a workspace (Required)

It is required to specify a workspace when running dotnet-format. Choosing a workspace determines which code files are considered for formatting.

- `--folder` - The folder to operate on. Cannot be used with the `--workspace` option.
- `--workspace` - The solution or project file to operate on. If a file is not specified, the command will search the current directory for one.

### Filter files to format

You can further narrow the list of files to be formatted by limiting to set of included files that are not excluded.

- `--include` - A comma separated list of relative file or folder paths to include in formatting.
- `--exclude` - A comma separated list of relative file or folder paths to exclude from formatting.

*Example:*

Other repos built as part of your project can be included using git submodules. These submodules likely contain their own .editorconfig files that are set as `root = true`. This
makes it difficult to validate formatting for your project as formatting mistakes in submodules are treated as errors.

The following command sets the repo folder as the workspace. It then includes the `./src` and `./tests` folders for formatting. The `submodule-a` folder is excluded from the formatting validation.

```console
dotnet format -f . --include ./src,./tests --exclude ./src/submodule-a --check
```

### Logging and Reports

- `--verbosity` - Set the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic]
- `--report` - Writes a json file to the given directory. Defaults to 'format-report.json' if no filename given.

### Validate formatting

- `--check` - Formats files without saving changes to disk. Terminates with a non-zero exit code (`2`) if any files were formatted.