---
title: Troubleshoot dotnetup
description: Diagnose and fix common dotnetup problems, such as missing SDKs, untracked installations, locked files, and corrupted state files.
ms.topic: troubleshooting
ms.date: 10/09/2026
---

# Troubleshoot dotnetup

This article describes common `dotnetup` problems and safe recovery steps.

## Collect diagnostic information

Before you troubleshoot a problem, collect this information:

- Show the `dotnetup` version, architecture, runtime identifier, and verified
  installations:

  ```dotnetcli
  dotnetup --info
  ```

- Show tracked requirements and installations, and verify the files on disk:

  ```dotnetcli
  dotnetup list
  ```

- Compare the stored environment configuration with the current environment:

  ```dotnetcli
  dotnetup env show
  ```

- Run the failed command again with detailed output. For example:

  ```dotnetcli
  dotnetup sdk install 10.0 --verbosity detailed
  ```

If the command uses `--manifest-path` or a custom installation root, include
those options when you collect diagnostics.

## Projects don't build or apps don't start after setup

**Symptom:** After you set up `dotnetup` in `everywhere` mode on Windows,
projects that built before now fail to build, or framework-dependent apps show
this error:

```output
You must install or update .NET to run this application.
```

**Cause:** In `everywhere` mode, the dotnetup-managed installation takes
precedence over the machine-wide installation in Program Files. SDKs and
runtimes that exist only in the machine-wide installation aren't available.

**Solution:** Install the required SDKs and runtimes in the dotnetup-managed
installation. To derive requirements from detected machine-wide installations,
run:

```dotnetcli
dotnetup sdk install --migrate-from-system
dotnetup runtime install --migrate-from-system
```

Migration downloads matching components through the normal install workflow.
It doesn't copy or modify the machine-wide installation.

Alternatively, change to `shell` or `none` mode. For more information, see
[Everywhere mode considerations](concepts/environment.md#everywhere-mode-considerations).

## dotnet doesn't use the dotnetup-managed installation

**Symptom:** The `dotnet` command in your terminal doesn't use the SDKs that
`dotnetup` installed.

**Solution:**

1. Open a new terminal. Environment changes don't affect terminals that are
   already open.
1. Run `dotnetup env show`. If it reports drift, run `dotnetup env set` to
   reapply the stored configuration.
1. To configure only the current terminal, evaluate `dotnetup env script`.

For Bash or zsh:

```bash
eval "$(dotnetup env script)"
```

For PowerShell:

```powershell
dotnetup env script --shell pwsh | Invoke-Expression
```

## An install path already contains an untracked installation

**Symptom:** An install command reports that the selected path contains a .NET
installation that the selected manifest doesn't track.

**Cause:** `dotnetup` doesn't mix tracked files with files that another tool
owns.

**Solution:** Use a new empty installation root, or remove the existing
installation if you own it. If another process owns the installation and you
must use `--untracked`, select a separate root that no tracked dotnetup
installation uses.

`--untracked` skips manifest recording, but it doesn't protect files placed
inside an already tracked root. Garbage collection can remove untracked
component directories that no tracked requirement needs.

## The dotnet executable is in use

**Symptom:** On Windows, an install command warns that it couldn't update the
`dotnet` executable because another process is using it.

**Solution:** Close running .NET apps, IDEs, terminals that are running
`dotnet`, and build servers. Then run the install command again. To make the
command fail instead of continuing with the existing executable, use
`--require-muxer-update`.

## Another dotnetup process is running

**Symptom:** A command reports that another `dotnetup` process is running and
waits for it to finish.

**Cause:** Installation-changing workflows use a shared process lock. Separate
installation roots and manifests isolate state, but they don't remove this
shared lock.

**Solution:** Wait for the other command to finish. Serialize independent
automation jobs when lock contention is unacceptable. If no command should be
running, inspect the reported processes before ending a process that stopped
responding.

## The manifest is corrupt

**Symptom:** A command reports that a manifest is corrupt or was modified
outside `dotnetup`.

**Solution:**

1. Note the exact manifest path in the error. It might be a custom manifest,
   not the default manifest.
1. Back up the manifest and its `.sha256` checksum before changing anything.
1. Restore a known-good manifest and checksum pair if you have one.
1. If no backup is available, move the corrupt pair aside and reinstall the
   required SDKs and runtimes into a new empty root with a new manifest.

Don't delete or reuse the old installation root until you have reconciled its
contents. Removing tracking state can make files appear unowned, and deleting
a shared root can remove unrelated files.

## The configuration file is corrupt

**Symptom:** A command warns that `dotnetup.config.json` couldn't be read.

**Solution:** Run `dotnetup env set` with the access mode that you want, or run
`dotnetup init` again. These commands write a new configuration.

## A shell profile contains a malformed dotnetup block

**Symptom:** An `env` command reports that a begin or end marker is missing
from the managed block in a shell profile.

**Solution:** Back up the profile, remove the incomplete dotnetup block or add
the missing marker, and run the command again.

## Elevation was canceled

**Symptom:** On Windows, a command that turns `everywhere` mode on or off says
that elevation was canceled.

**Cause:** Adding or removing the managed root from the system `PATH` requires
elevation.

**Solution:** Run the command again and approve the User Account Control
prompt. Switching from `everywhere` to `shell` or `none` still requires
elevation to remove the previous system `PATH` entry.

## A release manifest has expired

**Symptom:** An install or update fails during release-manifest verification
with the `ExpiredNow` failure code.

**Solution:**

1. Check the system date and time and correct the clock if necessary.
1. Retry the command. If a proxy serves cached release metadata, ask an
   administrator to check whether it serves an expired manifest.
1. If the clock is correct and the failure persists, report the problem with
   detailed command output.

Don't edit the manifest or change the clock to bypass expiration checks. For
more information, see
[Manifest expiration](concepts/download-verification.md#manifest-expiration).

## An installation is blocked by an IT policy

**Symptom:** An install of a daily or unlisted prerelease build fails because
an IT policy requires code-signed downloads.

**Solution:** Install a released version or ask an administrator to clear the
policy. For more information, see
[How dotnetup verifies downloads](concepts/download-verification.md#block-unsigned-downloads).

## Report a problem

Search the
[existing dotnetup issues](https://github.com/dotnet/sdk/issues?q=is%3Aissue%20label%3AArea-dotnetup).
If you don't find a match, open a new issue in the
[dotnet/sdk repository](https://github.com/dotnet/sdk/issues/new/choose).
Include the output of `dotnetup --info` and the failed command with
`--verbosity detailed`.

For questions and suggestions, use
[dotnetup discussions](https://github.com/dotnet/sdk/discussions/categories/dotnetup).

## See also

- [How dotnetup works](concepts/how-dotnetup-works.md)
- [dotnetup environment configuration](concepts/environment.md)
- [dotnetup command reference](reference/dotnetup.md)
