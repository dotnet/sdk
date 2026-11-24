---
title: dotnetup telemetry
description: Learn what usage data dotnetup collects, how to opt out, and how to suppress the first-run notice.
ms.topic: conceptual
ms.date: 10/09/2026
---

# dotnetup telemetry

`dotnetup` collects usage and failure data and sends it to Microsoft when you
run commands. The .NET team uses this data to understand how people use the
tool and to diagnose problems.

## How to opt out

Telemetry is on by default. To opt out, set the
`DOTNET_CLI_TELEMETRY_OPTOUT` environment variable to `1` or `true`.

This is the same variable that turns off telemetry for the .NET SDK and CLI.
For more information, see
[.NET SDK and .NET CLI telemetry](https://learn.microsoft.com/dotnet/core/tools/telemetry).

## First-run notice

The first time that you run `dotnetup`, it explains that the tool collects
usage data and shows how to opt out.

To hide this notice without turning off telemetry, set the `DOTNET_NOLOGO`
environment variable to `1` or `true`.

## Data points

`dotnetup` doesn't scan your source code or collect usernames, email addresses,
or the contents of `global.json`. Operation telemetry can still describe the
requested action and repository-related state, such as whether a
`global.json` file was present.

Common telemetry includes:

- A persistent device identifier and an identifier for the current session.
- The command, caller, exit code, duration, `dotnetup` version, and commit for
  development builds.
- Operating-system, kernel, runtime identifier, process architecture, output
  redirection, and container information.
- Whether the process runs in a continuous integration environment or through
  a detected LLM agent.
- The value of `DOTNET_CLI_TELEMETRY_PROFILE`, when you set that variable.

Installation telemetry can include:

- The component and requested and resolved versions.
- Whether an install path was explicit and its classification or source.
- Whether a `global.json` file was present.
- The existing-installation classification, migration state, and result.
- Lock and installation-stage results.

Command forwarding telemetry can include the request source and requested
version. File-system paths used for correlation are hashed or represented as
classifications rather than reported as customer-readable paths.

## Failure telemetry

When a command fails, telemetry can include the error type, category, code,
HTTP status, HResult, sanitized details, and stack trace. Exception messages
and path-bearing values are excluded or sanitized before transmission.

## Related environment variables

| Variable | Description |
| --- | --- |
| `DOTNET_CLI_TELEMETRY_OPTOUT` | Turns off telemetry when set to `1` or `true`. |
| `DOTNET_NOLOGO` | Hides the first-run notice when set to `1` or `true`. Telemetry stays on. |
| `DOTNET_CLI_TELEMETRY_PROFILE` | Adds the supplied profile label to telemetry. Don't put sensitive data in this value. |
| `DOTNET_CLI_TELEMETRY_STORAGE_PATH` | Changes the directory where `dotnetup` stores telemetry locally before upload. |
| `DOTNET_CLI_TELEMETRY_SHUTDOWN_TIMEOUT_MS` | In CI environments, changes the maximum time, in milliseconds, that `dotnetup` waits to send telemetry before exit. The default is 20,000. |

## Privacy

If you think telemetry collects sensitive data or is handled inappropriately,
file an issue in the
[dotnet/sdk repository](https://github.com/dotnet/sdk/issues).

For more information, see the
[Microsoft Privacy Statement](https://www.microsoft.com/privacy/privacystatement).

## See also

- [.NET SDK and .NET CLI telemetry](https://learn.microsoft.com/dotnet/core/tools/telemetry)
- [dotnetup overview](index.md)
