// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.Shell;

public class PowerShellEnvShellProvider : IEnvShellProvider
{
    public string ArgumentName => "pwsh";

    public string Extension => "ps1";

    public string? HelpDescription => "PowerShell Core (pwsh)";

    public override string ToString() => ArgumentName;

    public string GenerateEnvScript(string dotnetInstallPath, string? dotnetupDir = null, bool includeDotnet = true)
    {
        var escapedPath = dotnetInstallPath.Replace("'", "''", StringComparison.Ordinal);
        var escapedDotnetupDir = dotnetupDir?.Replace("'", "''", StringComparison.Ordinal);

        string pathExport;
        if (includeDotnet && escapedDotnetupDir is not null)
        {
            pathExport = $"$env:PATH = '{escapedDotnetupDir}' + [IO.Path]::PathSeparator + '{escapedPath}' + [IO.Path]::PathSeparator + $env:PATH";
        }
        else if (includeDotnet)
        {
            pathExport = $"$env:PATH = '{escapedPath}' + [IO.Path]::PathSeparator + $env:PATH";
        }
        else if (escapedDotnetupDir is not null)
        {
            pathExport = $"$env:PATH = '{escapedDotnetupDir}' + [IO.Path]::PathSeparator + $env:PATH";
        }
        else
        {
            pathExport = "";
        }

        if (!includeDotnet)
        {
            return
                $"""
                # This script adds dotnetup to your PATH
                {pathExport}
                """;
        }

        return
            $"""
            # This script configures the environment for .NET installed at {dotnetInstallPath}
            
            $env:DOTNET_ROOT = '{escapedPath}'
            {pathExport}
            """;
    }

    public IReadOnlyList<string> GetProfilePaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return [Path.Combine(home, ".config", "powershell", "Microsoft.PowerShell_profile.ps1")];
    }

    public string GenerateProfileEntry(string dotnetupPath, bool dotnetupOnly = false, string? dotnetInstallPath = null)
    {
        var escapedPath = dotnetupPath.Replace("'", "''", StringComparison.Ordinal);
        var flags = GetFlags(dotnetupOnly, dotnetInstallPath);
        return $"{ShellProfileManager.MarkerComment}\n& '{escapedPath}' print-env-script --shell pwsh{flags} | Invoke-Expression";
    }

    public string GenerateActivationCommand(string dotnetupPath, bool dotnetupOnly = false, string? dotnetInstallPath = null)
    {
        var escapedPath = dotnetupPath.Replace("'", "''", StringComparison.Ordinal);
        var flags = GetFlags(dotnetupOnly, dotnetInstallPath);
        return $"& '{escapedPath}' print-env-script --shell pwsh{flags} | Invoke-Expression";
    }

    private static string GetFlags(bool dotnetupOnly, string? dotnetInstallPath)
    {
        var flags = dotnetupOnly ? " --dotnetup-only" : "";
        if (!dotnetupOnly &&
            dotnetInstallPath is { Length: > 0 } installPath &&
            !DotnetupUtilities.PathsEqual(installPath, DotnetupPaths.DefaultDotnetInstallPath))
        {
            var escapedInstallPath = installPath.Replace("'", "''", StringComparison.Ordinal);
            flags += $" --dotnet-install-path '{escapedInstallPath}'";
        }

        return flags;
    }
}
