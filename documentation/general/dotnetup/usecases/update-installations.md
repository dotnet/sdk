# Updating SDK and Runtime Installations

`dotnetup` tracks your installations by channel (and by workspace if you have `global.json` files), so updating is straightforward. Run `dotnetup update` and every tracked channel resolves to its latest matching version.

## Quick Reference

```bash
# Update everything (all SDKs and runtimes)
dotnetup update

# Update SDKs only
dotnetup sdk update

# Update runtimes only
dotnetup runtime update

# Update and write new versions back to global.json files
dotnetup sdk update --update-global-json
```

## How Updates Work

When you install a channel like `10.0` or `latest`, dotnetup records that channel in its manifest. Running `dotnetup update` re-resolves each channel to its newest version and installs it if it's newer than what you already have.

```
$ dotnetup update
Downloading .NET SDK 10.0.103...  ████████████████████████ 100%
Installed .NET SDK 10.0.103 to C:\Users\you\.dotnet
Runtime 9.0.5 is already up to date.
ASP.NET Core 9.0.5 is already up to date.
```

If everything is current:

```
$ dotnetup update
Everything is up to date.
```

### What Gets Updated

| Channel Type | Update Behavior |
|-------------|----------------|
| `latest` | Updates to the newest stable .NET SDK |
| `lts` | Updates to the newest LTS release |
| `preview` | Updates to the newest preview/RC |
| `10.0` | Updates to the latest 10.0.x SDK |
| `10.0.1xx` | Updates within the 10.0.1xx feature band |
| `10.0.100` | **Never updates** — pinned to an exact version |

Pinned versions (fully-specified like `10.0.100`) are point-in-time snapshots. They will never be changed by `dotnetup update` because the channel matches exactly one version.

## Updating SDKs

### Update all tracked SDKs

```
$ dotnetup sdk update
Downloading .NET SDK 10.0.103...  ████████████████████████ 100%
Installed .NET SDK 10.0.103 to C:\Users\you\.dotnet
```

### Update SDKs and global.json

If your SDK was installed from a `global.json` file, you can update both the SDK and the `global.json` in one step:

```
$ dotnetup sdk update --update-global-json
Downloading .NET SDK 10.0.103...  ████████████████████████ 100%
Installed .NET SDK 10.0.103 to C:\Users\you\.dotnet
Updated C:\src\myproject\global.json: 10.0.100 → 10.0.103
```

This is useful for keeping your project's `global.json` pinned to a consistent minimum version while still using a rolling channel for installation tracking.

When updating SDKs that are managed via `global.json`, `dotnetup` will adhere to any roll-forward policies defined in the `global.json` file. If you want to 'bump' the version in `global.json` to the latest available, you can use the `--update-global-json` flag and `dotnetup` will take care of that for you as part of the update.

## Updating Runtimes

### Update all tracked runtimes

```
$ dotnetup runtime update
Downloading .NET Runtime 10.0.1...       ████████████████████████ 100%
Downloading ASP.NET Core Runtime 10.0.1... ████████████████████████ 100%
```

Runtime updates cover all tracked runtime components:
- **.NET Runtime** (`runtime`)
- **ASP.NET Core Runtime** (`aspnetcore`)
- **Windows Desktop Runtime** (`windowsdesktop`, Windows only)

### Best-effort updates

When updating multiple components, dotnetup continues even if one component fails. After processing all components, it reports the first failure:

```
$ dotnetup runtime update
Downloading .NET Runtime 10.0.1...       ████████████████████████ 100%
Error: Could not resolve channel '9.0' for ASP.NET Core.
Downloading Windows Desktop Runtime 10.0.1... ████████████████████████ 100%
2 update(s) failed; reporting the first failure.
```

This ensures that a transient failure on one component doesn't block updates to the others.

## Installing Runtimes

You can also install runtimes directly without an SDK:

```bash
# Install the latest .NET 10.0 runtime
dotnetup runtime install 10.0

# Install a specific runtime type
dotnetup runtime install aspnetcore@10.0
dotnetup runtime install windowsdesktop@9.0

# Install the latest runtime
dotnetup runtime install latest
```

The component spec format is `<type>@<channel>`, where `<type>` is one of:
- `runtime` (or omit the type for the default .NET runtime)
- `aspnetcore` / `aspnet`
- `windowsdesktop` / `desktop` (Windows only)

## Updating Across Multiple Install Roots

If you have installations in multiple directories, `dotnetup update` processes all of them:

```
$ dotnetup list

Installations (managed by dotnetup):

  C:\Users\you\.dotnet
    Tracked channels:
      SDK latest                          (source: explicit)

  D:\projects\.dotnet
    Tracked channels:
      SDK 10.0.1xx                        (source: D:\projects\global.json)

Total: 2

$ dotnetup update
Downloading .NET SDK 10.0.103...  ████████████████████████ 100%
Installed .NET SDK 10.0.103 to C:\Users\you\.dotnet
SDK 10.0.102 in D:\projects\.dotnet is already up to date.
```

To update only a specific install root:

```
$ dotnetup sdk update --install-path D:\projects\.dotnet
```

## Uninstalling

To stop tracking a channel and clean up unused installations:

```bash
# Remove the SDK channel "9.0" and clean up unused versions
dotnetup sdk uninstall 9.0

# Remove a runtime channel
dotnetup runtime uninstall aspnetcore@9.0
```

The uninstall command removes the tracking spec from the manifest and then garbage-collects any versions that are no longer needed by any remaining specs.

## Next Steps

- [Install SDKs with global.json](./install-with-global-json.md) — Let global.json drive your SDK installs
- [Try Daily Builds](./try-daily-builds.md) — Safely test pre-release .NET builds
