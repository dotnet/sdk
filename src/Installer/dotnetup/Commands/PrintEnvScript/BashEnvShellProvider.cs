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

    public string GenerateEnvScript(string dotnetInstallPath)
    {
        // Escape single quotes in the path for bash by replacing ' with '\''
        var escapedPath = dotnetInstallPath.Replace("'", "'\\''");

        return
            $"""
            #!/usr/bin/env bash
            # This script configures the environment for .NET installed at {dotnetInstallPath}
            # Source this script to add .NET to your PATH and set DOTNET_ROOT
            #
            # Note: If you had a different dotnet in PATH before sourcing this script,
            # you may need to run 'hash -d dotnet' to clear the cached command location.
            # When dotnetup modifies shell profiles directly, it will handle this automatically.

            export DOTNET_ROOT='{escapedPath}'
            export PATH='{escapedPath}':$PATH
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

    public string GenerateProfileEntry(string dotnetupPath)
    {
        var escapedPath = dotnetupPath.Replace("'", "'\\''");
        return $"{MarkerComment}\neval \"$('{escapedPath}' print-env-script --shell bash)\"";
    }

    public string GenerateActivationCommand(string dotnetupPath)
    {
        var escapedPath = dotnetupPath.Replace("'", "'\\''");
        return $"eval \"$('{escapedPath}' print-env-script --shell bash)\"";
    }
}
