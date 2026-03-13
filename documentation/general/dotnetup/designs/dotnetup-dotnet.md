# `dotnetup dotnet`

# Motivation

Manipulating the `PATH` environment variable can be tricky when Visual Studio and other installers are involved. These applications are automatically run with updates. They override the system level path on a regular basis which blocks `dotnetup` installs from being used.

To provide an experience during the prototype of `dotnetup` before any official product is changed to work well with `dotnetup`, we propose 'aliasing' or 'shadowing' dotnet commands via `dotnetup` as one option.

One downside to this is that IDE components whose processes call `dotnet` would still be broken by a change to the `PATH`. However, this prevents user interactions with terminals and scripts from being broken.

Another downside is that this requires scripts to be updated to call `dotnetup dotnet` instead of `dotnet`. For many individuals, this is a no-go.
This also adds the overhead of an additional process call.

Yet, until the `dotnet` muxer itself or .NET Installer can be modified, this provides a consistent way for the user to enforce their intended install of `dotnet` when running commands.

This also enables the `PATH` to have the admin install and for the two install types to co-exist - such that
`dotnetup` based installs can still be used by the local user. That makes `dotnetup` useful even when IT Admins prevent the user from overriding the system path, such that it can be used in tandem with admin installs. This also gives `dotnetup` full control over the process call, such that environment variables like `DOTNET_ROOT` can be set.

# Commands

`dotnetup dotnet <>`
`dotnetup do <>` (alias for the same thing)

Arguments in `<>` are forwarded transparently to `dotnet.exe` in the determined location which limits our ability to configure the command itself.
The `dotnet.exe` hive used will follow the logic `dotnetup` uses for installation location. (e.g. `global.json` vs `dotnetup hive` priority.)

# Technical Details

### Permissions

The subprocess inherits the current elevation level. We considered de-elevating when `dotnetup` runs under elevation, but decided against it: it's confusing for an admin prompt to de-elevate, and workload commands often require elevation. There is also more risk trying obscure methods to de-elevate. The subprocess will run with whatever privileges `dotnetup` currently has.

### Return values

We should return with the return value of `dotnet` and mimic that behavior.

### Interactive Mode

The command uses `shell=true` (UseShellExecute=false with shell dispatch) so that interactive commands (e.g. `dotnetup dotnet interactive`) work correctly with stdin/stdout/stderr streaming. This also supports shell redirection techniques such as `<<` and piping.

### Environment Settings

The spawned process should modify the `PATH` and set `DOTNET_ROOT` to the value of the determined `dotnet.exe` location, so that `runtime` based interactions (debug, test, run) can work as expected. This would override any custom `DOTNET_ROOT` to ensure the hive is used, for scenarios like when an admin install is side by side with `dotnetup` installs. It should also preserve the `cwd` of the shell and the other environment variables contained within. Since we use `shell=true`, environment variables are expanded by the shell and we don't need to call `ExpandEnvironmentVariables` manually.

When cross-architecture support is added, we should consider setting variables such as `DOTNET_ROOT_x64`.

### Future: Native AOT In-Process Invocation

If `dotnetup` is published as native AOT, we could potentially invoke the dotnet hive's `hostfxr` directly in-process instead of spawning a subprocess. This would eliminate the process overhead entirely. This is a future work item — for now, we use the subprocess approach.
