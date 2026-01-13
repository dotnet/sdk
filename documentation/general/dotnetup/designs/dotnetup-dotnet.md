# `dotnetup dotnet`

# Motivation

Manipulating the `PATH` environment variable can be tricky when Visual Studio and other installers are involved. These applications are automatically run with updates. They override the system level path on a regular basis which blocks `dotnetup` installs from being used.

To provide an experience during the prototype of `dotnetup` before any official product is changed to work well with `dotnetup`, we propose 'aliasing' or 'shadowing' dotnet commands via `dotnetup` as one option.

One downside to this is that IDE components whose processes call `dotnet` would still be broken by a change to the `PATH`. However, this prevents user interactions with terminals and scripts from being broken.

Another downside is that this requires scripts to be updated to call `dotnetup dotnet` instead of `dotnet`. For many individuals, this is a no-go.
This also adds the overhead of an additional process call.

Yet, until the `dotnet` muxer itself or .NET Installer can be modified, this provides a consistent way for the user to enforce their intended install of `dotnet` when running commands.

This also enables the `PATH` to have the admin install and for the two install types to co-exist to some degree - such that `dotnetup` based installs can still be used by the local user. This also gives `dotnetup` full control over the process call, such that environment variables like `DOTNET_ROOT` can be set.

# Commands

`dotnetup dotnet <>`
`dotnetup do <>` (alias for the same thing)

Arguments in `<>` are forwarded transparently to `dotnet.exe` in the determined location which limits our ability to configure the command itself.
The `dotnet.exe` hive used will follow the logic `dotnetup` uses for installation location. (e.g. `global.json` vs `dotnetup hive` priority.)

# Technical Details

### Permissions

We should avoid unintended consequences where an elevated terminal or admin prompt running `dotnetup` runs a `user` executable with elevation.
One approach would be to sign check dotnet every single time before hand. This is very expensive.
So alternatively, when we spawn `dotnet`, we should attempt to revoke privileges if `dotnetup` is running under elevation.

On Unix, it is rare to have a 'shell' that's running as admin - instead, users deliberately run under `sudo` or `su`.

For Windows,

The standard approach for this on Windows is surprisingly to have explorer.exe launch the process, because explorer.exe is unelevated.
This implementation can serve as reference for doing so, which we would do if and only if `dotnetup` is currently running under `elevation` (using the workloads check `Microsoft.DotNet.Cli.Installer.Windows.WindowsUtils.IsAdministrator()`): https://github.com/microsoft/nodejstools/blob/main/Nodejs/Product/Nodejs/SharedProject/SystemUtilities.cs#L18

We could provide `dotnetup -a dotnet` as a way to allow running the `dotnet` command with the admin rights, assuming it is needed (e.g. working in a protected folder.)

### Return values

We should return with the return value of `dotnet` and mimic that behavior.

### Environment Settings

The spawned process should modify the `PATH` and set `DOTNET_ROOT` to the value of the determined `dotnet.exe` location, so that `runtime` based interactions (debug, test, run) can work as expected. This would override any custom `DOTNET_ROOT` to ensure the hive is used, for scenarios like when an admin install is side by side with `dotnetup` installs. It should also preserve the `cwd` of the shell and the other environment variables contained within, which need to be expanded (`ExpandEnvironmentVariables`) to run without the overhead of an additional shell.

Using `shell=false` does mean that shell redirection techniques such as `<<` may not work as intended, which could be a point we revisit.

When cross-architecture support is added, we should consider setting variables such as `DOTNET_ROOT_x64`.
