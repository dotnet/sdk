# Unix Environment Setup for dotnetup

## Overview

This document describes the design for setting up the .NET environment via initialization scripts using the `dotnetup print-env-script` command. This is the first step toward enabling automatic user profile configuration for Unix as described in [issue #51582](https://github.com/dotnet/sdk/issues/51582). Note that this also supports PowerShell and thus Windows, but on Windows the main method of configuring the environment will be to set environment variables which are stored in the registry instead of written by initialization scripts.

## Background

The dotnetup tool manages multiple .NET installations in a local user hive. For .NET to be accessible from the command line, the installation directory must be:
1. Added to the `PATH` environment variable
2. Set as the `DOTNET_ROOT` environment variable

On Unix systems, this requires modifying shell configuration files (like `.bashrc`, `.zshrc`, etc.) or sourcing environment setup scripts.

## Design Goals

1. **Non-invasive**: Don't automatically modify user shell configuration files without explicit consent
2. **Flexible**: Support multiple shells (bash, zsh, PowerShell)
3. **Reversible**: Users should be able to easily undo environment changes
4. **Single-file execution**: Generate scripts that can be sourced or saved for later use
5. **Discoverable**: Make it easy for users to understand how to configure their environment

## The `dotnetup print-env-script` Command

### Command Structure

```bash
dotnetup print-env-script [--shell <shell>] [--dotnet-install-path <path>]
```

### Options

- `--shell` / `-s`: The target shell for which to generate the environment script
  - Supported values: `bash`, `zsh`, `pwsh`
  - Optional: If not specified, automatically detects the current shell from the `$SHELL` environment variable
  - On Windows, defaults to PowerShell (`pwsh`)

- `--dotnet-install-path` / `-d`: The path to the .NET installation directory
  - Optional: If not specified, uses the default user install path (`~/.local/share/dotnet` on Unix)

### Usage Examples

#### Auto-detect current shell
```bash
dotnetup print-env-script
```

#### Generate and source in one command
```bash
source <(dotnetup print-env-script)
```

#### Explicitly specify shell
```bash
dotnetup print-env-script --shell zsh
```

#### Save script for later use
```bash
dotnetup print-env-script --shell bash > ~/.dotnet-env.sh
# Later, in .bashrc or manually:
source ~/.dotnet-env.sh
```

#### Use custom installation path
```bash
dotnetup print-env-script --dotnet-install-path /opt/dotnet
```

## Generated Script Format

The command generates shell-specific scripts that:
1. Set the `DOTNET_ROOT` environment variable to the installation path
2. Prepend the installation path to the `PATH` environment variable

### Bash/Zsh Example
```bash
#!/usr/bin/env bash
# This script configures the environment for .NET installed at /home/user/.local/share/dotnet
# Source this script to add .NET to your PATH and set DOTNET_ROOT

export DOTNET_ROOT='/home/user/.local/share/dotnet'
export PATH='/home/user/.local/share/dotnet':$PATH
```

### PowerShell Example
```powershell
# This script configures the environment for .NET installed at /home/user/.local/share/dotnet
# Source this script (dot-source) to add .NET to your PATH and set DOTNET_ROOT
# Example: . ./dotnet-env.ps1

$env:DOTNET_ROOT = '/home/user/.local/share/dotnet'
$env:PATH = '/home/user/.local/share/dotnet' + [IO.Path]::PathSeparator + $env:PATH
```

## Implementation Details

### Provider Model

The implementation uses a provider model similar to `System.CommandLine.StaticCompletions`, making it easy to add support for additional shells in the future.

**Interface**: `IEnvShellProvider`
```csharp
public interface IEnvShellProvider
{
    string ArgumentName { get; }           // Shell name for CLI (e.g., "bash")
    string Extension { get; }               // File extension (e.g., "sh")
    string? HelpDescription { get; }        // Help text for the shell
    string GenerateEnvScript(string dotnetInstallPath);
}
```

**Implementations**:
- `BashEnvShellProvider`: Generates bash-compatible scripts
- `ZshEnvShellProvider`: Generates zsh-compatible scripts
- `PowerShellEnvShellProvider`: Generates PowerShell Core scripts

### Shell Detection

The command automatically detects the current shell when the `--shell` option is not provided:

1. **On Unix**: Reads the `$SHELL` environment variable and extracts the shell name from the path
   - Example: `/bin/bash` → `bash`
2. **On Windows**: Defaults to PowerShell (`pwsh`)

### Security Considerations

**Path Escaping**: All installation paths are properly escaped to prevent shell injection vulnerabilities:
- **Bash/Zsh**: Uses single quotes with `'\''` escaping for embedded single quotes
- **PowerShell**: Uses single quotes with `''` escaping for embedded single quotes

This ensures that paths containing special characters, spaces, or shell metacharacters are handled safely.

## Advantages of Generated Scripts

As noted in the discussion, generating scripts dynamically has several advantages over using embedded resource files:

1. **Single-file execution**: Users can source the script directly from the command output without needing to extract files
2. **Flexibility**: Easy to customize the installation path or add future features
3. **No signing required**: Generated text doesn't require code signing, unlike downloaded executables or scripts
4. **Immediate availability**: No download or extraction step needed
5. **Transparency**: Users can easily inspect what the script does by running the command

## Shell Profile Modification

Building on `print-env-script`, dotnetup can automatically modify shell profile files so that `.NET` is available in every new terminal session. This is triggered in two ways:

1. **`sdk install --interactive`** — When the user confirms "set as default install?", dotnetup persists the environment configuration to shell profiles in addition to setting environment variables for the current process.
2. **`defaultinstall user`** — Standalone command that configures the default install, including shell profile modification on Unix.

After either operation, dotnetup prints a command the user can paste into the current terminal to activate `.NET` immediately, since profile changes only take effect in new shells.

### Which Profile Files Are Modified

| Shell | Files modified | Rationale |
|-------|---------------|-----------|
| **bash** | `~/.bashrc` (always) + the first existing of `~/.bash_profile` / `~/.profile` (creates `~/.profile` if neither exists) | `.bashrc` covers Linux terminals (non-login shells). The login profile covers macOS Terminal and SSH sessions. We never create `~/.bash_profile` to avoid shadowing an existing `~/.profile`. |
| **zsh** | `~/.zshrc` (created if needed) | Covers all interactive zsh sessions. `~/.zshenv` is avoided because on macOS, `/etc/zprofile` runs `path_helper` which resets PATH after `.zshenv` loads. |
| **pwsh** | `~/.config/powershell/Microsoft.PowerShell_profile.ps1` (creates directory and file if needed) | Standard `$PROFILE` path on Unix. |

### Profile Entry Format

Each profile file gets a marker comment and an eval line:

**Bash / Zsh:**
```bash
# dotnetup
eval "$(/path/to/dotnetup print-env-script --shell bash)"
```

**PowerShell:**
```powershell
# dotnetup
& /path/to/dotnetup print-env-script --shell pwsh | Invoke-Expression
```

The path to dotnetup is the full path to the running binary (`Environment.ProcessPath`).

### Reversibility

- The `# dotnetup` marker comment immediately before the eval line identifies the addition.
- To remove: find the marker line and the line after it, remove both.
- Before modifying any file, dotnetup creates a backup (e.g., `~/.bashrc.dotnetup-backup`).

### Provider Model

The `IEnvShellProvider` interface is extended with two methods so each shell provider owns its profile knowledge:

- `GetProfilePaths()` — Returns the list of profile file paths to modify for the shell.
- `GenerateProfileEntry(string dotnetupPath)` — Generates the marker comment and eval line.

A `ShellProfileManager` class coordinates the file I/O: adding and removing entries, creating backups, and ensuring idempotency (entries are not duplicated if already present).

## Future Work

1. **`defaultinstall admin` on Unix**: System-wide configuration (e.g., `/etc/profile.d/`) is not yet supported.
2. **Additional shells**: Support for fish, tcsh, and other shells.
3. **Environment validation**: Commands to verify that the environment is correctly configured.

## Related Issues

- [Issue #51582](https://github.com/dotnet/sdk/issues/51582): Parent issue tracking user profile modification on Unix
- [dotnet/designs dnvm-e2e-experience](https://github.com/dotnet/designs/tree/dnvm-e2e-experience/proposed): Design proposal for local .NET hive management

## Testing

The implementation includes comprehensive tests:
- Parser tests for command validation
- Shell provider tests for script generation
- Security tests for special character handling
- Help documentation tests
- Shell profile manager tests for add/remove/idempotency/backup behavior

All tests ensure that the generated scripts are syntactically correct and properly escape paths.
