# Installing SDKs with global.json

`dotnetup` integrates with [`global.json`](https://learn.microsoft.com/dotnet/core/tools/global-json) files so you can install exactly the right .NET SDK version for a project — just by running `dotnetup` in the project directory.

## How It Works

When you run `dotnetup sdk install` (or just `dotnetup install`) without specifying a channel, dotnetup looks for a `global.json` file in the current directory or any parent directory. If one is found, dotnetup reads the SDK version and `rollForward` policy to determine which channel to install.

```
$ cat global.json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestPatch"
  }
}

$ dotnetup sdk install
SDK 10.0.1xx will be installed since C:\src\myproject\global.json specifies that version.
Downloading .NET SDK 10.0.102...  ████████████████████████ 100%
Installed .NET SDK 10.0.102 to C:\Users\you\.dotnet
```

## Channel Resolution from global.json

The combination of `sdk.version` and `sdk.rollForward` determines the channel that dotnetup tracks:

| global.json `rollForward` | SDK version in global.json | dotnetup Channel | What Gets Installed |
|---------------------------|---------------------------|-----------------|---------------------|
| `latestPatch` (default)   | `10.0.100`                | `10.0.1xx`      | Latest in the 10.0.1xx feature band |
| `latestFeature`           | `10.0.100`                | `10.0`          | Latest 10.0.x SDK |
| `latestMinor`             | `10.0.100`                | `10`            | Latest 10.x SDK |
| `latestMajor`             | `10.0.100`                | `latest`        | Latest stable SDK |
| `disable`                 | `10.0.100`                | `10.0.100`      | Exactly 10.0.100 (pinned) |
| `patch`                   | `10.0.100`                | `10.0.100`      | Exactly 10.0.100 (pinned) |
| `feature`                 | `10.0.100`                | `10.0.100`      | Exactly 10.0.100 (pinned) |
| `minor`                   | `10.0.100`                | `10.0.100`      | Exactly 10.0.100 (pinned) |
| `major`                   | `10.0.100`                | `10.0.100`      | Exactly 10.0.100 (pinned) |

> **Note:** The `rollForward` policies with a `latest` prefix (`latestPatch`, `latestFeature`, `latestMinor`, `latestMajor`) tell dotnetup to track a rolling channel — you'll get the newest SDK within that scope when you install or update. The policies without `latest` (e.g. `patch`, `feature`, `minor`, `major`, `disable`) pin the channel to the exact version specified in `global.json`, meaning `dotnetup update` will not change it.

## Examples

### Install the SDK specified by global.json

Navigate to your project directory and run:

```
$ cd ~/src/my-project
$ dotnetup sdk install
SDK 10.0.1xx will be installed since /home/you/src/my-project/global.json specifies that version.
Downloading .NET SDK 10.0.102...  ████████████████████████ 100%
Installed .NET SDK 10.0.102 to /home/you/.dotnet
```

### Install an explicit channel alongside global.json

You can override the global.json by specifying a channel explicitly:

```
$ dotnetup sdk install 9.0
Downloading .NET SDK 9.0.304...  ████████████████████████ 100%
Installed .NET SDK 9.0.304 to C:\Users\you\.dotnet
```

### Install and update global.json

Use `--update-global-json` to have dotnetup write the resolved version back to your `global.json`:

```
$ cat global.json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestPatch"
  }
}

$ dotnetup sdk install --update-global-json
SDK 10.0.1xx will be installed since C:\src\myproject\global.json specifies that version.
Downloading .NET SDK 10.0.102...  ████████████████████████ 100%
Installed .NET SDK 10.0.102 to C:\Users\you\.dotnet

$ cat global.json
{
  "sdk": {
    "version": "10.0.102",
    "rollForward": "latestPatch"
  }
}
```

### Install multiple channels at once

You can install multiple SDK channels in a single command:

```
$ dotnetup sdk install 9.0 10.0
Downloading .NET SDK 9.0.304...  ████████████████████████ 100%
Downloading .NET SDK 10.0.100... ████████████████████████ 100%
Installed .NET SDK 9.0.304 to C:\Users\you\.dotnet
Installed .NET SDK 10.0.100 to C:\Users\you\.dotnet
```

### Install when no global.json exists

When there's no `global.json` and no channel is specified, dotnetup defaults to the `latest` channel:

```
$ dotnetup sdk install
Downloading .NET SDK 10.0.100...  ████████████████████████ 100%
Installed .NET SDK 10.0.100 to C:\Users\you\.dotnet
```

## Viewing What's Tracked

After installing, you can see which channels dotnetup is tracking and which versions are installed:

```
$ dotnetup list

Installations (managed by dotnetup):

  C:\Users\you\.dotnet

    Tracked channels:
      SDK 10.0.1xx                        (source: C:\src\myproject\global.json)
      SDK 9.0                             (source: explicit)

    Installed versions:
      SDK 9.0.304                         (x64)
      SDK 10.0.102                        (x64)

Total: 2
```

Notice that the `source` column shows whether a channel came from a `global.json` file or was specified explicitly on the command line.

## How global.json Search Works

dotnetup searches for `global.json` by walking up from the current directory toward the filesystem root — the same behavior as the `dotnet` CLI itself. This means:

```
/home/you/src/my-project/          ← looks here first
/home/you/src/                     ← then here
/home/you/                         ← then here
/home/                             ← and so on...
/
```

dotnetup uses the first `global.json` found while walking upward. If that file contains an SDK version, dotnetup derives the channel from it.

## Next Steps

- [Update SDK and Runtime Installations](./update-installations.md) — Keep your channels up to date
- [Try Daily Builds](./try-daily-builds.md) — Test pre-release .NET builds safely
