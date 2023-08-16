# Workload History

When .NET SDK workloads are updated, it is possible that the new versions of the workloads won't work (if we shipped a bug) or won't be available (if the manifest updates are available but the packs themselves are not).  Right now it is hard to fix such a state.  We will add a `dotnet workload history` command that will show a history of the workload installation actions that have been run via the .NET CLI, and add new options on the `dotnet workload update` command that will allow rolling back to a previously installed version of workloads from the history.

## Recording installation actions

When a CLI command is run which modifies the installed workloads or updates the manifests, we will write a record to the workload history log.  The workload commands this applies to include `install`, `update`, `uninstall`, `repair`, and `restore`.  Each workload history record will include the following information:

- Date/time command was run
- Date/time command completed
- Top-level workload command run (such as install, update, or uninstall)
- Workloads passed as arguments
- Contents of rollback file passed in, if any
- Full command line
- Success or failure, and error message on failure
- Workload state before and after command run
  - Manifest versions (rollback file)
  - Installed workloads

Each installation record will be stored as a file in a folder specific to the SDK feature band.  For MSI-based installs, this folder will be `%PROGRAMDATA%\dotnet\workloads\{sdk-band}\history\`.  For File-based installs, it will be `{DOTNET ROOT}\metadata\workloads\{sdk-band}\history\`.

## `dotnet workload history`

The `dotnet workload history` command will show the history of workload installation commands.  The output will be similar to the following format:

|ID|Date|Command|Workloads|
|--|----|-------|---------|
|3|2023-02-01|Update|android, maui, wasm-tools|
| |2023-02-01|Unknown|Unknown|
|2|2023-01-05|Install|maui, wasm-tools|
|1|2023-01-01|Install|android|

The ID will be an identifier for the record which will start at 1 for the oldest record and increment from there.  Each record includes the workload state before and after the command was run.  The history command will compare the state after each command was run with the state recorded before the next command record.  If there is a difference, then something changed the state that was not recorded, most likely a Visual Studio installation, or an update to the .NET SDK.  In such a case an "unknown" line will be included in the output to represent this difference.

## `dotnet workload update --from-history

A `--from-history` option will be added to the `dotnet workload update` command, which will update to a state from the workload history.  This will require one of two new parameters, `--before` or `--after`, to be specified.  By default the command will update the manifests to the versions specified, as well as install or uninstall workloads to match the workloads that were installed at the specified point.  A `--manifests-only` option will be supported which will update the manifests, but not change the current workloads that are installed (this may result in installing or uninstalling packs to match the new manifest versions).

For example:

`dotnet workload update --from-history --before 3 --manifests-only` - This will roll back to the manifests before operation 3 in the workload history.

`dotnet workload update --from-history --after 2` - This will roll back to the manifests and workloads installed after operation 2 in workload history.

