// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.PrintEnvScript;

public class PowerShellEnvShellProvider : IEnvShellProvider
{
    private const string MarkerComment = "# dotnetup";

    public string ArgumentName => "pwsh";

    public string Extension => "ps1";

    public string? HelpDescription => "PowerShell Core (pwsh)";

    public override string ToString() => ArgumentName;

    public string GenerateEnvScript(string dotnetInstallPath, string? dotnetupDir = null)
    {
        // Escape single quotes in the path for PowerShell by replacing ' with ''
        var escapedPath = dotnetInstallPath.Replace("'", "''");
        var pathExport = $"$env:PATH = '{escapedPath}' + [IO.Path]::PathSeparator + $env:PATH";

        if (dotnetupDir is not null)
        {
            var escapedDotnetupDir = dotnetupDir.Replace("'", "''");
            pathExport = $"$env:PATH = '{escapedDotnetupDir}' + [IO.Path]::PathSeparator + '{escapedPath}' + [IO.Path]::PathSeparator + $env:PATH";
        }

        return
            $"""
            # This script configures the environment for .NET installed at {dotnetInstallPath}
            # Source this script (dot-source) to add .NET to your PATH and set DOTNET_ROOT
            # Example: . ./dotnet-env.ps1
            
            $env:DOTNET_ROOT = '{escapedPath}'
            {pathExport}
            """;
    }

    public IReadOnlyList<string> GetProfilePaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return [Path.Combine(home, ".config", "powershell", "Microsoft.PowerShell_profile.ps1")];
    }

    public string GenerateProfileEntry(string dotnetupPath)
    {
        var escapedPath = dotnetupPath.Replace("'", "''");
        return $"{MarkerComment}\n& '{escapedPath}' print-env-script --shell pwsh | Invoke-Expression";
    }

    public string GenerateActivationCommand(string dotnetupPath)
    {
        var escapedPath = dotnetupPath.Replace("'", "''");
        return $"& '{escapedPath}' print-env-script --shell pwsh | Invoke-Expression";
    }
}
