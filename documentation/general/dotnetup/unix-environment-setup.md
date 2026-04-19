# Unix Environment Setup for dotnetup

## Overview

dotnetup automatically configures the Unix shell environment so that .NET is available in every new terminal session. This involves modifying shell profile files to set the `PATH` and `DOTNET_ROOT` environment variables. The same mechanism also supports PowerShell on any platform.

On Windows the primary method is registry-based environment variables, which is handled separately. This document focuses on the Unix (and PowerShell) profile-based approach.

## How the Environment Gets Configured

There are two primary ways the environment is configured:

### 1. During `dotnetup sdk install` / `dotnetup runtime install`

When running interactively (the default in a terminal) **and no explicit `--install-path` is provided**, the install commands flow through the walkthrough. The walkthrough asks how the user wants to use dotnetup (for example, keeping it isolated vs. configuring the shell profile so `dotnet` works directly in new terminals).

Choosing the shell-profile option in the walkthrough is what corresponds to making the dotnetup-managed user install the default:

- **On Windows**: Environment variables are set in the registry and updated for the current process.
- **On Unix**: Shell profile files are modified so .NET is available in future terminal sessions. Since profile changes only take effect in new shells, dotnetup also prints an activation command for the current terminal:

  ```
  To start using .NET in this terminal, run:
    eval "$('/home/user/.local/share/dotnetup/dotnetup' print-env-script --shell bash)"
  ```

If the user already has a saved path preference, or if the command is non-interactive / uses an explicit `--install-path`, the walkthrough prompt is skipped and dotnetup uses the existing configuration or the explicit path directly. If shell auto-detection is wrong or unavailable, run `dotnetup init --shell bash|zsh|pwsh` (or `defaultinstall` / `print-env-script` with `--shell`) before installing.

### 2. `dotnetup defaultinstall`

A standalone command that explicitly configures (or reconfigures) the default .NET install:

```bash
# Set up user-level default install (modifies shell profiles)
dotnetup defaultinstall user

# Switch to system-managed .NET (removes DOTNET_ROOT from profiles, keeps dotnetup on PATH)
dotnetup defaultinstall system
```

**`defaultinstall user`** on Unix:
1. Detects the current shell
2. Modifies the appropriate shell profile files
3. Prints an activation command for the current terminal

**`defaultinstall system`** on Unix:
- Replaces existing profile entries with dotnetup-only entries (keeps dotnetup on PATH but removes `DOTNET_ROOT` and dotnet from `PATH`), since the system package manager owns the .NET installation.

## Shell Profile Modification

### Which Profile Files Are Modified

| Shell | Files modified | Rationale |
|-------|---------------|-----------|
| **bash** | `~/.bashrc` (always) + the first existing of `~/.bash_profile` / `~/.profile` (creates `~/.profile` if neither exists) | `.bashrc` covers Linux terminals (non-login shells). The login profile covers macOS Terminal and SSH sessions. We never create `~/.bash_profile` to avoid shadowing an existing `~/.profile`. |
| **zsh** | `$ZDOTDIR/.zshrc` when `ZDOTDIR` is set; otherwise `~/.zshrc` (created if needed) | Covers all interactive zsh sessions. `~/.zshenv` is avoided because on macOS, `/etc/zprofile` runs `path_helper` which resets PATH after `.zshenv` loads. |
| **pwsh** | `~/.config/powershell/Microsoft.PowerShell_profile.ps1` (creates directory and file if needed) | Standard PowerShell profile path on Unix. |

The home directory used for these lookups comes from the user's current environment (`HOME`, or `USERPROFILE` / `Environment.SpecialFolder.UserProfile` as a fallback). dotnetup fails with a clear error if it cannot determine a writable profile location.

### Profile Entry Format

Each profile file gets a dotnetup-managed block with explicit begin/end markers:

**Bash / Zsh:**
```bash
# dotnetup: begin
if [ -x '/path/to/dotnetup' ]; then
    eval "$('/path/to/dotnetup' print-env-script --shell bash)"
fi
# dotnetup: end
```

**PowerShell:**
```powershell
# dotnetup: begin
if (Test-Path -LiteralPath '/path/to/dotnetup' -PathType Leaf)
{
    $dotnetupScript = & '/path/to/dotnetup' print-env-script --shell pwsh | Out-String
    if (-not [string]::IsNullOrWhiteSpace($dotnetupScript))
    {
        Invoke-Expression $dotnetupScript
    }
}
# dotnetup: end
```

The path to dotnetup is the full path to the running binary (`Environment.ProcessPath`). The `--dotnet-install-path` argument is only included in generated profile entries when dotnetup is configured to use a non-default install root.

### Safe updates

When updating an existing profile file, dotnetup writes the new content to a separate file and then swaps it into place. This avoids leaving a partially written profile behind if the update is interrupted, and it does not keep a persistent backup file after a successful update.

### Reversibility

To remove the environment configuration manually, remove the full block from `# dotnetup: begin` through `# dotnetup: end` in each profile file.

### Idempotency

If a profile file already contains a dotnetup-managed block, the entry is updated in place rather than duplicated.

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

1. **On Unix**: Reads the `$SHELL` environment variable, resolves symlinks when possible, and extracts the shell name from the resulting path (for example `/bin/bash` → `bash`)
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
    string GenerateProfileEntry(string dotnetupPath, bool dotnetupOnly = false, string? dotnetInstallPath = null);
    string GenerateActivationCommand(string dotnetupPath, bool dotnetupOnly = false, string? dotnetInstallPath = null);
}
```

**Implementations**: `BashEnvShellProvider`, `ZshEnvShellProvider`, `PowerShellEnvShellProvider`

### ShellDetection

`ShellDetection.GetCurrentShellProvider()` resolves the user's current shell to the matching `IEnvShellProvider`. On Windows it returns the PowerShell provider; on Unix it reads `$SHELL`, resolves symlinks when possible, and allows callers to override detection with `--shell`.

### ShellProfileManager

`ShellProfileManager` coordinates profile file modifications:
- `AddProfileEntries(provider, dotnetupPath, dotnetupOnly, dotnetInstallPath)` — creates or updates the managed begin/end block in place, creates backups, and can thread through a custom install path
- `RemoveProfileEntries(provider)` — finds and removes the full managed block

`defaultinstall system` uses `AddProfileEntries(..., dotnetupOnly: true)` to switch the managed entry into dotnetup-only mode.

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
