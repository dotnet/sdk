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

## Future Work

This command provides the foundation for future enhancements:

1. **Automatic profile modification**: Add a command to automatically update shell configuration files (`.bashrc`, `.zshrc`, etc.) with user consent
2. **Profile backup**: Create backups of shell configuration files before modification
3. **Uninstall/removal**: Add commands to remove dotnetup configuration from shell profiles
4. **Additional shells**: Support for fish, tcsh, and other shells
5. **Environment validation**: Commands to verify that the environment is correctly configured

## Related Issues

- [Issue #51582](https://github.com/dotnet/sdk/issues/51582): Parent issue tracking user profile modification on Unix
- [dotnet/designs dnvm-e2e-experience](https://github.com/dotnet/designs/tree/dnvm-e2e-experience/proposed): Design proposal for local .NET hive management

## Testing

The implementation includes comprehensive tests:
- Parser tests for command validation
- Shell provider tests for script generation
- Security tests for special character handling
- Help documentation tests

All tests ensure that the generated scripts are syntactically correct and properly escape paths.
