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

- Use `--no-progress` when logs do not support terminal progress.
- Use `--verbosity normal` for normal automation.
- Use `--verbosity detailed` to diagnose resolution or installation.
- Check the command exit code. Update commands can continue after an
  individual failure and report failure after processing other requirements.

## Coordinate writers

Separate installation roots and manifests isolate tracking state, but
installation-changing workflows still use a shared process lock. Serialize
concurrent install, update, or uninstall jobs when lock contention is
unacceptable.

## See also

- [dotnetup list](../reference/dotnetup-list.md)
- [Manage custom installation roots](manage-custom-installation-roots.md)
- [Update tracked installations](update-installations.md)
