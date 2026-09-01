---
title: dotnetup environment variables
description: Environment variables supported by dotnetup.
ms.topic: reference
ms.date: 08/14/2026
---

# dotnetup environment variables

This article lists the environment variables that `dotnetup` supports. It
doesn't list variables that only affect a `dotnet` process started by
`dotnetup dotnet`.

## dotnetup variables

| Variable | Description |
| --- | --- |
| `DOTNET_DOTNETUP_DATA_DIR` | Overrides the base directory for dotnetup state, including its configuration, installation manifest, download cache, default managed .NET installation, and telemetry notice. |
| `DOTNET_CLI_TELEMETRY_STORAGE_PATH` | Overrides the directory where telemetry is persisted before upload. Despite its `DOTNET_CLI_` prefix, this variable isn't currently part of the public [.NET environment-variable reference](https://learn.microsoft.com/dotnet/core/tools/dotnet-environment-variables). dotnetup supports it because dotnetup and the .NET CLI share telemetry storage. |
| `DOTNET_CLI_TELEMETRY_SHUTDOWN_TIMEOUT_MS` | Sets the nonnegative number of milliseconds that dotnetup waits for telemetry delivery before a CI process exits. The default is 20,000 milliseconds. A shorter timeout reduces exit latency but can make delivery less reliable; a longer timeout favors delivery. |
| `DOTNETUP_TELEMETRY_FORCE_LOCAL_DELIVERY` | Set to `1` or `true` to use local persist-and-detached-drain delivery even when dotnetup detects CI. This avoids waiting for inline CI delivery, but telemetry can remain on disk until the detached drainer or a later invocation delivers it. |

## Shared .NET CLI variables

dotnetup honors these supported .NET CLI variables. Follow the linked
canonical documentation for their contracts.

| Variable | Effect on dotnetup |
| --- | --- |
| [`DOTNET_CLI_TELEMETRY_OPTOUT`](https://learn.microsoft.com/dotnet/core/tools/telemetry#how-to-opt-out) | Disables dotnetup telemetry. |
| [`DOTNET_NOLOGO`](https://learn.microsoft.com/dotnet/core/tools/telemetry#disclosure) | Suppresses the dotnetup first-run telemetry notice without disabling telemetry. |
| [`DOTNET_CLI_UI_LANGUAGE`](https://learn.microsoft.com/dotnet/core/tools/dotnet-environment-variables#dotnet_cli_ui_language) | Selects the language used for dotnetup output. |
| `VSLANG` | Selects the UI language by Visual Studio locale identifier when `DOTNET_CLI_UI_LANGUAGE` isn't set. `DOTNET_CLI_UI_LANGUAGE` takes precedence. |

dotnetup also uses the .NET CLI's
[CI detection](https://learn.microsoft.com/dotnet/core/tools/telemetry#continuous-integration-detection)
to select its telemetry delivery mode and its
[LLM-agent detection](https://learn.microsoft.com/dotnet/core/tools/telemetry#llm-detection)
for telemetry metadata. The provider variables listed in those references are
owned by their providers, not by dotnetup.

## Operating-system and shell variables

dotnetup follows these established operating-system and shell variables:

| Variable | Effect on dotnetup |
| --- | --- |
| `PATH` | `dotnetup dotnet` prepends the managed .NET installation to the child process's inherited path. The `dotnetup env` commands can also manage dotnetup and .NET entries in the user environment. |
| `DOTNET_ROOT` | `dotnetup env` can inspect and manage the Windows user value. `dotnetup dotnet` sets the value for its child process to the selected managed installation. |
| `SHELL` | Identifies the current shell when an `env` command needs to select a shell profile or script format. |
| `HOME`, `USERPROFILE` | Locates the user home directory for shell profiles. `USERPROFILE` is used on Windows and `HOME` on other operating systems. |
| `ZDOTDIR` | Locates the Z shell configuration directory. |
| `XDG_CONFIG_HOME` | Locates the Fish configuration directory. |
| `LC_ALL`, `LC_MESSAGES`, `LANG` | Select dotnetup's UI language on platforms that use POSIX locales when neither `DOTNET_CLI_UI_LANGUAGE` nor `VSLANG` selects a supported language. They are checked in the order shown. |

For diagnostics, implementation details, and test hooks, see
[dotnetup contributor environment variables](../developer-environment-variables.md).
