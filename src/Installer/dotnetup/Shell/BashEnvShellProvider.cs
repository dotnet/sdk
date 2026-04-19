// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Shell;

public class BashEnvShellProvider : IEnvShellProvider
{
    public string ArgumentName => "bash";

    public string Extension => "sh";

    public string? HelpDescription => "Bash shell";

    public override string ToString() => ArgumentName;

    public string GenerateEnvScript(string dotnetInstallPath, string dotnetupDir = "", bool includeDotnet = true)
    {
        var escapedPath = ShellProviderHelpers.EscapePosixPath(dotnetInstallPath);
        var pathExport = ShellProviderHelpers.BuildPosixPathExport(escapedPath, dotnetupDir, includeDotnet);

        if (!includeDotnet)
        {
            return
                $"""
                #!/usr/bin/env bash
                {ShellProviderHelpers.GetDotnetupOnlyComment()}
                {pathExport}
                hash -d dotnet 2>/dev/null
                hash -d dotnetup 2>/dev/null
                """;
        }

        return
            $"""
            #!/usr/bin/env bash
            {ShellProviderHelpers.GetEnvironmentConfigurationComment(dotnetInstallPath)}

            export DOTNET_ROOT='{escapedPath}'
            {pathExport}
            hash -d dotnet 2>/dev/null
            hash -d dotnetup 2>/dev/null
            """;
    }

    public IReadOnlyList<string> GetProfilePaths()
    {
        var home = ShellProviderHelpers.GetUserHomeDirectoryOrThrow();
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

    public string GenerateProfileEntry(string dotnetupPath, bool dotnetupOnly = false, string? dotnetInstallPath = null)
    {
        var escapedPath = ShellProviderHelpers.EscapePosixPath(dotnetupPath);
        var flags = ShellProviderHelpers.GetCommandFlags(dotnetupOnly, dotnetInstallPath, ShellProviderHelpers.EscapePosixPath);
        var command = ShellProviderHelpers.AppendArguments($"'{escapedPath}' print-env-script --shell bash", flags);
        return $"eval \"$({command})\"";
    }

    public string GenerateActivationCommand(string dotnetupPath, bool dotnetupOnly = false, string? dotnetInstallPath = null)
    {
        var escapedPath = ShellProviderHelpers.EscapePosixPath(dotnetupPath);
        var flags = ShellProviderHelpers.GetCommandFlags(dotnetupOnly, dotnetInstallPath, ShellProviderHelpers.EscapePosixPath);
        var command = ShellProviderHelpers.AppendArguments($"'{escapedPath}' print-env-script --shell bash", flags);
        return $"eval \"$({command})\"";
    }
}
