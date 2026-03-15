// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.PrintEnvScript;

public class ZshEnvShellProvider : IEnvShellProvider
{
    public string ArgumentName => "zsh";

    public string Extension => "zsh";

    public string? HelpDescription => "Zsh shell";

    public override string ToString() => ArgumentName;

    public string GenerateEnvScript(string dotnetInstallPath, string? dotnetupDir = null, bool includeDotnet = true)
    {
        var escapedPath = dotnetInstallPath.Replace("'", "'\\''");
        var escapedDotnetupDir = dotnetupDir?.Replace("'", "'\\''");

        string pathExport;
        if (includeDotnet && escapedDotnetupDir is not null)
        {
            pathExport = $"export PATH='{escapedDotnetupDir}':'{escapedPath}':$PATH";
        }
        else if (includeDotnet)
        {
            pathExport = $"export PATH='{escapedPath}':$PATH";
        }
        else if (escapedDotnetupDir is not null)
        {
            pathExport = $"export PATH='{escapedDotnetupDir}':$PATH";
        }
        else
        {
            pathExport = "";
        }

        if (!includeDotnet)
        {
            return
                $"""
                #!/usr/bin/env zsh
                # This script adds dotnetup to your PATH
                {pathExport}
                rehash 2>/dev/null
                """;
        }

        return
            $"""
            #!/usr/bin/env zsh
            # This script configures the environment for .NET installed at {dotnetInstallPath}

            export DOTNET_ROOT='{escapedPath}'
            {pathExport}
            rehash 2>/dev/null
            """;
    }

    public IReadOnlyList<string> GetProfilePaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return [Path.Combine(home, ".zshrc")];
    }

    public string GenerateProfileEntry(string dotnetupPath, bool dotnetupOnly = false)
    {
        var escapedPath = dotnetupPath.Replace("'", "'\\''");
        var flags = dotnetupOnly ? " --dotnetup-only" : "";
        return $"{ShellProfileManager.MarkerComment}\neval \"$('{escapedPath}' print-env-script --shell zsh{flags})\"";
    }

    public string GenerateActivationCommand(string dotnetupPath, bool dotnetupOnly = false)
    {
        var escapedPath = dotnetupPath.Replace("'", "'\\''");
        var flags = dotnetupOnly ? " --dotnetup-only" : "";
        return $"eval \"$('{escapedPath}' print-env-script --shell zsh{flags})\"";
    }
}
