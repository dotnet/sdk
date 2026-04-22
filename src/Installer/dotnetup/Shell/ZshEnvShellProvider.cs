// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Shell;

public class ZshEnvShellProvider : IEnvShellProvider
{
    public string ArgumentName => "zsh";

    public string Extension => "zsh";

    public string? HelpDescription => "Zsh shell";

    public override string ToString() => ArgumentName;

    public string GenerateEnvScript(string dotnetInstallPath, string dotnetupDir = "", bool includeDotnet = true)
    {
        var escapedPath = ShellProviderHelpers.EscapePosixPath(dotnetInstallPath);
        var pathExport = ShellProviderHelpers.BuildPosixPathExport(escapedPath, dotnetupDir, includeDotnet);

        if (!includeDotnet)
        {
            return
                $"""
                {ShellProviderHelpers.GetDotnetupOnlyComment(ArgumentName)}
                {pathExport}
                rehash 2>/dev/null
                """;
        }

        return
            $"""
            {ShellProviderHelpers.GetEnvironmentConfigurationComment(ArgumentName, dotnetInstallPath)}

            export DOTNET_ROOT='{escapedPath}'
            {pathExport}
            rehash 2>/dev/null
            """;
    }

    public IReadOnlyList<string> GetProfilePaths()
    {
        var zshDirectory = ShellProviderHelpers.GetZshConfigurationDirectoryOrThrow();
        return [Path.Combine(zshDirectory, ".zshrc")];
    }

    public string GenerateProfileEntry(string dotnetupPath, bool dotnetupOnly = false, string? dotnetInstallPath = null)
    {
        var flags = ShellProviderHelpers.GetCommandFlags(dotnetupOnly, dotnetInstallPath, ShellProviderHelpers.EscapePosixPath);
        return ShellProviderHelpers.BuildPosixProfileEntry(dotnetupPath, "zsh", flags);
    }

    public string GenerateActivationCommand(string dotnetupPath, bool dotnetupOnly = false, string? dotnetInstallPath = null)
    {
        var flags = ShellProviderHelpers.GetCommandFlags(dotnetupOnly, dotnetInstallPath, ShellProviderHelpers.EscapePosixPath);
        return ShellProviderHelpers.BuildPosixActivationCommand(dotnetupPath, "zsh", flags);
    }
}
