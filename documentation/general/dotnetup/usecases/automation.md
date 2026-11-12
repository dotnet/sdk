---
title: Use dotnetup in automation
description: Use deterministic dotnetup commands and machine-readable output in scripts and CI.
ms.topic: how-to
ms.date: 08/07/2026
---

# Use dotnetup in automation

`dotnetup` disables first-use onboarding when it detects CI or redirected
output. Use explicit commands and options so scripts do not depend on terminal
detection.

## Install for a build

Install a rolling feature band:

```dotnetcli
dotnetup sdk install 10.0.1xx --no-progress --interactive false
dotnetup dotnet test -- --logger trx
```

Install an exact version when a build must stay pinned:

```dotnetcli
dotnetup sdk install 10.0.103 --no-progress --interactive false
```

Exact requirements are not changed by update commands.

## Use a repository-local root

```dotnetcli
dotnetup sdk install 10.0.1xx --install-path .dotnet --no-progress --interactive false
```

Run the local executable directly or activate it with `dotnetup env script`.
The forwarding command uses the default dotnetup-managed .NET installation
root.

## Use dotnetup in GitHub Actions

The following workflow installs `dotnetup`, installs the SDK required by the
repository's `global.json`, and runs tests with the same repository-local
installation:

```yaml
name: build

on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    env:
      DOTNET_ROOT: ${{ github.workspace }}/.dotnet
    steps:
      - uses: actions/checkout@v4

      - name: Install dotnetup
        run: |
          curl -fsSL https://aka.ms/dotnetup/get-dotnetup.sh | bash
          echo "$HOME/.dotnetup" >> "$GITHUB_PATH"

      - name: Install the .NET SDK
        run: dotnetup sdk install --install-path "$DOTNET_ROOT" --no-progress --interactive false

      - name: Test
        run: "$DOTNET_ROOT/dotnet" test
```

The download script doesn't change `PATH`. The workflow adds the `dotnetup`
directory to `GITHUB_PATH` so later steps can run `dotnetup`.

The explicit installation root ensures that the install and test steps use the
same `dotnet`, even when `global.json` contains `sdk.paths`. When you omit an
SDK channel, `dotnetup sdk install` uses the nearest `global.json`. If no file
exists, it installs the `latest` channel.

## Read state as JSON

```dotnetcli
dotnetup list --format json --no-verify
```

Omit `--no-verify` when the automation must check that recorded files are
present and valid.

## Keep output useful

- Progress prints as plain lines with periodic percentages when output is
  redirected or in CI (`--progress lines`). Use `--no-progress` (or
  `--progress none`) for minimal, deterministic log output: nothing while
  the install runs, then one line per component in the order you requested:

  ```text
  Installing dotnet (runtime) 8.0.27, 9.0.16 to D:\sdk\.dotnet...
    dotnet (runtime) 8.0.27  already present, skipped
    dotnet (runtime) 9.0.16  installed (30.4 MB)
  Installed at D:\sdk\.dotnet.
  ```
- Use `--progress json` when a program, such as an editor extension, shows
  its own progress UI. See [Progress events](#progress-events).
- Use `--verbosity normal` for normal automation.
- Use `--verbosity detailed` to diagnose resolution or installation.
- Check the command exit code. Update commands can continue after an
  individual failure and report failure after processing other requirements.

## Progress events

With `--progress json`, stdout carries only progress events, one JSON object
per line. All other output, including warnings, errors, and the final summary,
goes to stderr. Use the exit code to decide success.

```json
{"version":1,"event":"started","task":1,"description":"Downloading .NET SDK 10.0.103"}
{"version":1,"event":"progress","task":1,"description":"Downloading .NET SDK 10.0.103 (68.2 MB / 272.6 MB)","percent":25}
{"version":1,"event":"finished","task":1,"description":"Downloaded .NET SDK 10.0.103 (272.6 MB / 272.6 MB)"}
```

| Field | Description |
| --- | --- |
| `version` | Event format version. It increases only for incompatible changes. |
| `event` | `started`, `progress`, or `finished`. |
| `task` | Identifies the task within one command. Concurrent installs interleave tasks. |
| `description` | Display text for the task's current state. |
| `percent` | Whole-number completion, on `progress` events only. A task can finish without reporting every percentage. |

Ignore fields you do not recognize; later versions can add fields.

## Coordinate writers

Separate installation roots and manifests isolate tracking state, but
installation-changing workflows still use a shared process lock. Serialize
concurrent install, update, or uninstall jobs when lock contention is
unacceptable.

## See also

- [dotnetup list](../reference/dotnetup-list.md)
- [Manage custom installation roots](manage-custom-installation-roots.md)
- [Update tracked installations](update-installations.md)
