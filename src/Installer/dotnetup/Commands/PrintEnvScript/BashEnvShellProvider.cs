// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.PrintEnvScript;

public class BashEnvShellProvider : IEnvShellProvider
{
    private const string MarkerComment = "# dotnetup";

    public string ArgumentName => "bash";

    public string Extension => "sh";

    public string? HelpDescription => "Bash shell";

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
                #!/usr/bin/env bash
                # This script adds dotnetup to your PATH
                {pathExport}
                """;
        }

        return
            $"""
            #!/usr/bin/env bash
            # This script configures the environment for .NET installed at {dotnetInstallPath}
            #
            # Note: If you had a different dotnet in PATH before sourcing this script,
            # you may need to run 'hash -d dotnet' to clear the cached command location.
            # When dotnetup modifies shell profiles directly, it will handle this automatically.

            export DOTNET_ROOT='{escapedPath}'
            {pathExport}
            """;
    }

    public IReadOnlyList<string> GetProfilePaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var paths = new List<string> { Path.Combine(home, ".bashrc") };

        // For login shells, use the first existing of .bash_profile / .profile.
        // Never create .bash_profile — it would shadow an existing .profile.
        string bashProfile = Path.Combine(home, ".bash_profile");
        string profile = Path.Combine(home, ".profile");

        if (File.Exists(bashProfile))
        {
            paths.Add(bashProfile);
        }
        else
        {
            // Use .profile (will be created if it doesn't exist)
            paths.Add(profile);
        }

        return paths;
    }

    public string GenerateProfileEntry(string dotnetupPath, bool dotnetupOnly = false)
    {
        var escapedPath = dotnetupPath.Replace("'", "'\\''");
        var flags = dotnetupOnly ? " --dotnetup-only" : "";
        return $"{MarkerComment}\neval \"$('{escapedPath}' print-env-script --shell bash{flags})\"";
    }

    public string GenerateActivationCommand(string dotnetupPath, bool dotnetupOnly = false)
    {
        var escapedPath = dotnetupPath.Replace("'", "'\\''");
        var flags = dotnetupOnly ? " --dotnetup-only" : "";
        return $"eval \"$('{escapedPath}' print-env-script --shell bash{flags})\"";
    }
}
