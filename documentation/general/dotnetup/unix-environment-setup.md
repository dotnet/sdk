# Unix Environment Setup for dotnetup

## Overview

dotnetup automatically configures the Unix shell environment so that .NET is available in every new terminal session. This involves modifying shell profile files to set the `PATH` and `DOTNET_ROOT` environment variables. The same mechanism also supports PowerShell on any platform.

On Windows the primary method is registry-based environment variables, which is handled separately. This document focuses on the Unix (and PowerShell) profile-based approach.

## How the Environment Gets Configured

There are two primary ways the environment is configured:

### 1. During `dotnetup sdk install` / `dotnetup runtime install`

When running interactively (the default in a terminal), the install command prompts the user to set the default install if one is not already configured:

```
Do you want to set the install path (~/.local/share/dotnet) as the default dotnet install?
This will update the PATH and DOTNET_ROOT environment variables. [Y/n]
```

If the user confirms (or passes `--set-default-install` explicitly):

- **On Windows**: Environment variables are set in the registry and updated for the current process.
- **On Unix**: Shell profile files are modified so .NET is available in future terminal sessions. Since profile changes only take effect in new shells, dotnetup also prints an activation command for the current terminal:

  ```
  To start using .NET in this terminal, run:
    eval "$('/home/user/.local/share/dotnetup/dotnetup' print-env-script --shell bash)"
  ```

If the default install is already fully configured and matches the install path, the prompt is skipped entirely.

### 2. `dotnetup defaultinstall`

A standalone command that explicitly configures (or reconfigures) the default .NET install:

```bash
# Set up user-level default install (modifies shell profiles)
dotnetup defaultinstall user

# Switch to admin/system-managed .NET (removes DOTNET_ROOT from profiles, keeps dotnetup on PATH)
dotnetup defaultinstall admin
```

**`defaultinstall user`** on Unix:
1. Detects the current shell
2. Modifies the appropriate shell profile files
3. Prints an activation command for the current terminal

**`defaultinstall admin`** on Unix:
- Replaces existing profile entries with dotnetup-only entries (keeps dotnetup on PATH but removes `DOTNET_ROOT` and dotnet from `PATH`), since the system package manager owns the .NET installation.

## Shell Profile Modification

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
eval "$('/path/to/dotnetup' print-env-script --shell bash)"
```

**PowerShell:**
```powershell
# dotnetup
& '/path/to/dotnetup' print-env-script --shell pwsh | Invoke-Expression
```

The path to dotnetup is the full path to the running binary (`Environment.ProcessPath`).

### Backups

Before modifying an existing profile file, dotnetup creates a backup (e.g., `~/.bashrc.dotnetup-backup`). This allows the user to restore the file if needed.

### Reversibility

To remove the environment configuration, find the `# dotnetup` marker comment and the line immediately after it in each profile file, and remove both lines. The backup files can be used as a reference.

### Idempotency

If a profile file already contains the `# dotnetup` marker, the entry is not duplicated.

## The `print-env-script` Command

`print-env-script` is the low-level building block that generates shell-specific environment scripts. It is called internally by profile entries and activation commands, but can also be used standalone for custom setups, CI pipelines, or when you want to source the environment without modifying profile files.

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

#### Eval directly (one-time, current terminal only)
```bash
eval "$(dotnetup print-env-script)"
```

#### Explicitly specify shell
```bash
dotnetup print-env-script --shell zsh
```

#### Save script for later use
```bash
dotnetup print-env-script --shell bash > ~/.dotnet-env.sh
# Later, in .bashrc or manually:
. ~/.dotnet-env.sh
```

#### Use custom installation path
```bash
dotnetup print-env-script --dotnet-install-path /opt/dotnet
```

### Generated Script Format

The command generates shell-specific scripts that:
1. Set the `DOTNET_ROOT` environment variable to the installation path
2. Prepend the installation path to the `PATH` environment variable
3. Clear the shell's cached command location for `dotnet` to pick up the new PATH

**Bash/Zsh Example:**
```bash
#!/usr/bin/env bash
# This script configures the environment for .NET installed at /home/user/.local/share/dotnet

export DOTNET_ROOT='/home/user/.local/share/dotnet'
export PATH='/home/user/.local/share/dotnetup':'/home/user/.local/share/dotnet':$PATH
hash -d dotnet 2>/dev/null
hash -d dotnetup 2>/dev/null
```

**PowerShell Example:**
```powershell
# This script configures the environment for .NET installed at /home/user/.local/share/dotnet

$env:DOTNET_ROOT = '/home/user/.local/share/dotnet'
$env:PATH = '/home/user/.local/share/dotnetup' + [IO.Path]::PathSeparator + '/home/user/.local/share/dotnet' + [IO.Path]::PathSeparator + $env:PATH
```

### Shell Detection

When `--shell` is not specified, the command automatically detects the current shell:

1. **On Unix**: Reads the `$SHELL` environment variable and extracts the shell name from the path (e.g., `/bin/bash` → `bash`)
2. **On Windows**: Defaults to PowerShell (`pwsh`)

### Security Considerations

All installation paths are properly escaped to prevent shell injection vulnerabilities:
- **Bash/Zsh**: Uses single quotes with `'\''` escaping for embedded single quotes
- **PowerShell**: Uses single quotes with `''` escaping for embedded single quotes

This ensures that paths containing special characters, spaces, or shell metacharacters are handled safely.

## Implementation Details

### Provider Model

The implementation uses a provider model, making it easy to add support for additional shells in the future.

**Interface**: `IEnvShellProvider`
```csharp
public interface IEnvShellProvider
{
    string ArgumentName { get; }
    string Extension { get; }
    string? HelpDescription { get; }
    string GenerateEnvScript(string dotnetInstallPath, string? dotnetupDir = null, bool includeDotnet = true);
    IReadOnlyList<string> GetProfilePaths();
    string GenerateProfileEntry(string dotnetupPath, bool dotnetupOnly = false);
    string GenerateActivationCommand(string dotnetupPath, bool dotnetupOnly = false);
}
```

**Implementations**: `BashEnvShellProvider`, `ZshEnvShellProvider`, `PowerShellEnvShellProvider`

### ShellDetection

`ShellDetection.GetCurrentShellProvider()` resolves the user's current shell to the matching `IEnvShellProvider`. On Windows it returns the PowerShell provider; on Unix it reads `$SHELL`.

### ShellProfileManager

`ShellProfileManager` coordinates profile file modifications:
- `AddProfileEntries(provider, dotnetupPath)` — appends entries, creates backups, skips if already present
- `RemoveProfileEntries(provider)` — finds and removes marker + eval lines
- `ReplaceProfileEntries(provider, dotnetupPath, dotnetupOnly)` — removes then adds (used by `defaultinstall admin`)

## Future Work

1. **System-wide configuration on Unix**: Writing to system-wide locations like `/etc/profile.d/` for admin installs is not yet supported.
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
