# Getting Started with dotnetup

`dotnetup` is the .NET toolchain manager for user-level installs. It lets you install, update, and manage .NET SDKs and runtimes without needing administrator privileges or system package managers.

## Prerequisites

- **Windows**, **macOS**, or **Linux**
- A terminal (like bash, zsh, or PowerShell (Core))
- No administrator / root access required for Isolation or Terminal Mode. Replacement Mode on Windows requires administrator privileges.

## Download dotnetup

The easiest way to download `dotnetup` is to use the installation script:

```bash
curl -fsSL https://aka.ms/dotnetup/get-dotnetup.sh | bash
```

```pwsh
iwr https://aka.ms/dotnetup/get-dotnetup.ps1 | iex
```

These scripts will download the latest version of `dotnetup` and install it in your user directory, then print instructions to update your $PATH so that `dotnetup` is available in your terminal.

## First-Time Setup

When you run `dotnetup` for the first time (or run `dotnetup init` explicitly), it walks you through an interactive setup flow:

```
$ dotnetup
╭─────────────────────────────────────────────────╮
│ dotnetup v0.1.3-dev                             │
│ .NET toolchain manager for user level installs. │
╰─────────────────────────────────────────────────╯

Welcome to dotnetup!

dotnetup updates and groups installations using dotnetup channels.

Select an example channel to get started: (Enter to confirm)
> latest       (suggested)  Latest stable release  → 10.0.300
  none          I'll tell you what to install later.
  lts           Long Term Support  → 10.0.300
  preview       Latest preview  → 11.0.100-preview.4.26230.115
  10.0          Major.Minor channel  → 10.0.300

(use ↑↓ arrows)
```

### Step 1: Choose a Channel

Channels determine which version of .NET to install and how it gets updated. Pick one that matches your needs:

| Channel     | What It Installs | Update Behavior |
|-------------|-----------------|-----------------|
| `latest`    | The newest stable .NET SDK | Updates to the latest GA release |
| `lts`       | The current Long Term Support release | Updates within the LTS line |
| `preview`   | The latest preview/RC release | Updates to newer previews |
| `10.0`      | The latest SDK for .NET 10.0 | Updates within the 10.0 major.minor |
| `10.0.1xx`  | The latest SDK in the 10.0.1xx feature band | Updates within the feature band |
| `10.0.100`  | Exactly SDK 10.0.100 | Never updates (pinned) |
| `11.0.100-preview.6.26277.104` (or any other nightly build number)     | A specific version number of a daily VMR builds | Never updates |
| `none`      | Skips the initial install | You can install later with `dotnetup sdk install` |

In general, there are two kinds of channels: **version-based** and **feature-based**.
- **Version-based** channels specify an exact version or a range of versions - `10`,  `10.0.2xx`, and so on
- **Feature-based** channels group versions by their characteristics - `preview`, `lts`, and so on

### Step 2: Choose How to Access .NET

Next, dotnetup asks how you'd like to use the managed .NET installation:

```
How would you like to use dotnetup?

  Isolation Mode
  Use 'dotnetup dotnet' to consume installs managed by dotnetup.
  New installs can be used alongside your existing installs.

> Terminal Mode (Suggested)
  Configure the current shell profile to use installs managed by dotnetup.
  Only applications launched from the shell will leverage dotnetup installs.

  Replacement Mode      (Windows only)
  The system will be configured to use dotnetup installs over any other installs.
```

| Mode | How It Works | Best For |
|------|-------------|----------|
| **Isolation Mode** | Run .NET via `dotnetup dotnet <command>` | Users who want to keep their system .NET as-is |
| **Terminal Mode** | Updates your shell profile (`.bashrc`, `.zshrc`, or PowerShell `$PROFILE`) so `dotnet` resolves to the dotnetup-managed install | Most developers (suggested) |
| **Replacement Mode** | Modifies system PATH and DOTNET_ROOT (Windows only, requires admin) | Users who want dotnetup to be the default everywhere |

In general, we think most developers will want to use **Terminal Mode**. This ensures that the `dotnetup`-managed .NET installations are used by default in most contexts.  Visual Studio users *may* want to use **Replacement Mode** to ensure that the .NET SDK is used by default even in Visual Studio, but this will require both administrator permissions and monthly maintenance as Visual Studio re-installs .NET SDK bundles.

### Step 3: Migrate Existing Installs (Optional)

If you chose Terminal Mode or Replacement Mode and have existing system-level .NET installations that are installed to the default locations, dotnetup will offer to track matching versions:

```
You have existing system-managed .NET installs in C:\Program Files\dotnet.
  SDK 10.0.100
  Runtime 10.0.0
  ASP.NET Core 10.0.0
Do you want dotnetup to install matching versions in its managed directory? [y/n]
```

Accepting this migration downloads the same versions into the dotnetup-managed directory so that your existing projects continue to work seamlessly after the switch.

### Step 4: Installation Completes

```
Downloading .NET SDK 10.0.100...  ████████████████████████ 100%
Installed .NET SDK 10.0.100 to <dotnet install root>\.dotnet

Your shell profile has been updated. Restart your terminal or source your profile
to use 'dotnet' directly.
Setup complete!
```

## After Setup

Once setup is complete, you can verify the installation:

```
# If using Terminal Mode, restart your terminal first, then:
$ dotnet --version
10.0.100

# If using Isolation Mode:
$ dotnetup dotnet --version
10.0.100

# View what dotnetup is managing:
$ dotnetup list

Installations (managed by dotnetup):

  C:\Users\you\.dotnet

    Tracked channels:
      SDK latest                          (source: explicit)

    Installed versions:
      SDK 10.0.100                        (x64)

Total: 1
```

## Re-Running Setup

You can re-run the setup flow at any time with:

```
$ dotnetup init
```

This lets you change your path preference (Isolation → Terminal, etc.) or install additional .NET versions.

## Next Steps

- [Install SDKs with global.json](./usecases/install-with-global-json.md) — Install the right SDK version for a project automatically
- [Update SDK and Runtime Installations](./usecases/update-installations.md) — Keep your .NET installations current
- [Try Daily Builds](./usecases/try-daily-builds.md) — Safely test pre-release .NET builds
