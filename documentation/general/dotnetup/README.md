# Get started with dotnetup

`dotnetup` is a cross-platform toolchain manager for user-level .NET
installations. It installs, updates, and removes .NET SDKs and runtimes without
using a system package manager.

## Prerequisites

- Windows, macOS, or Linux.
- A terminal.
- Bash on macOS or Linux, or PowerShell on Windows, to run the download script.

The default installation and all access modes use your user profile. They do
not require administrator access. A custom installation path can require
additional file permissions.

## Download dotnetup

The download scripts install the latest `preview` build by default. They verify
the downloaded executable with its SHA-512 checksum and install it in
`~/.dotnetup`.

On macOS or Linux, run:

```bash
curl -fsSL https://aka.ms/dotnet/dotnetup/preview/get-dotnetup.sh | bash
```

On Windows, save and run the PowerShell script:

```powershell
$script = Join-Path $env:TEMP 'get-dotnetup.ps1'
Invoke-WebRequest https://aka.ms/dotnet/dotnetup/preview/get-dotnetup.ps1 -OutFile $script
& $script
```

To install the latest `daily` build on macOS or Linux, run:

```bash
curl -fsSL https://aka.ms/dotnet/dotnetup/daily/get-dotnetup.sh |
  bash -s -- --quality daily
```

To install the latest `daily` build on Windows, run:

```powershell
$script = Join-Path $env:TEMP 'get-dotnetup-daily.ps1'
Invoke-WebRequest https://aka.ms/dotnet/dotnetup/daily/get-dotnetup.ps1 -OutFile $script
& $script -Quality daily
```

> [!IMPORTANT]
> Daily builds have not completed release validation and are not code-signed.
> Use daily builds only for short-lived testing.

The scripts print instructions to add `dotnetup` to `PATH`. Open a new terminal
after you apply the instructions.

## Run first-time setup

Run the interactive setup:

```dotnetcli
> dotnetup init
Welcome to dotnetup!

SDK Channel: latest
Mode: <recommended-mode> (Suggested)

<recommended-mode> modifies <configuration-target> to set PATH and DOTNET_ROOT to prefer <install-path>.

Would you like to install .NET with the recommended settings?
> Yes, proceed with defaults and install

Installing SDK <resolved-version> to <install-path>...
<download and installation progress>
Installed at <install-path>:
  SDK <resolved-version>
<mode-specific environment guidance>
Setup complete!
```

The paths, progress display, and environment guidance depend on your system and
the selected mode.

The recommended mode is Everywhere Mode on Windows. On macOS and Linux, it is
Terminal Mode when `dotnetup` detects a supported shell. It is Isolation Mode
when shell detection is not available.

Select **No, customize setup** to choose the SDK channel, access mode, and
migration options.

### Choose an SDK channel

An SDK channel tells `dotnetup` which SDK to install and how to update it.

| Channel form | Example | Result |
| --- | --- | --- |
| Latest stable | `latest` | Latest active stable SDK |
| Latest LTS | `lts` | Latest active stable LTS SDK |
| Latest preview | `preview` | Latest preview SDK |
| Latest daily | `daily` | Latest available daily SDK |
| Major | `10` | Latest SDK for the specified major version |
| Major and minor | `10.0` | Latest SDK for the specified major and minor version |
| Feature band | `10.0.1xx` | Latest SDK in the specified feature band |
| Exact version | `10.0.103` | Only the specified SDK version |
| No initial SDK | `none` | Skip installation during setup |

Exact versions do not advance during updates. For all accepted forms, see
[dotnetup channels and versions](concepts/channels.md).

### Choose how to access .NET

The access mode controls how terminals and applications find the managed
`dotnet` command.

| Mode | Behavior |
| --- | --- |
| Isolation Mode | Use `dotnetup dotnet <command>`. Existing .NET installations remain the default. |
| Terminal Mode | Update the selected shell profile. Applications launched from that shell use the managed installation. |
| Everywhere Mode | Update the Windows user environment and shell profile. New terminals and user applications use the managed installation. Windows only. |

Terminal Mode supports Bash, Z shell, Fish, PowerShell Core, and Windows
PowerShell.

For more information, see
[dotnetup environment configuration](concepts/environment.md).

### Migrate existing installations

Setup can find SDKs and runtimes in the system-managed .NET directory. It can
install matching native-architecture versions in the dotnetup-managed
directory. This keeps existing projects working after you change the access
mode.

## Verify setup

Open a new terminal after setup changes your environment.

For Terminal Mode or Everywhere Mode, run:

```dotnetcli
dotnet --version
dotnetup list
```

For Isolation Mode, run:

```dotnetcli
dotnetup dotnet --version
dotnetup list
```

Run `dotnetup init` again to change the setup.

## Public documentation

- [dotnetup overview](index.md)
- [Table of contents](toc.yml)
- [Core concepts](concepts/how-dotnetup-works.md)
- [Release channels](channels/preview.md)
- [CLI reference](reference/dotnetup.md)
- [Scenarios](usecases/install-with-global-json.md)

## Maintainer documentation

- [Design notes](designs/)
- [How dotnetup is included in the SDK](dotnetup_in_sdk.md)
- [Release engineering](releasing.md)
- [Signature verification](signature-verification.md)

The command reference follows the generated runtime help. Command handlers and
tests verify product behavior. Hidden compatibility and elevation commands are
implementation details. They are not part of the public command reference.
