// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Shell;

public class FishEnvShellProvider : IEnvShellProvider
{
    public string ArgumentName => "fish";

    public string Extension => "fish";

    public string? HelpDescription => "Fish shell";

    public override string ToString() => ArgumentName;

    public string GenerateEnvScript(string dotnetInstallPath, string dotnetupDir = "", bool includeDotnet = true)
    {
        var escapedPath = ShellProviderHelpers.EscapeFishPath(dotnetInstallPath);
        var pathExport = ShellProviderHelpers.BuildFishPathExport(escapedPath, dotnetupDir, includeDotnet);

        if (!includeDotnet)
        {
            return
                $"""
                {ShellProviderHelpers.GetDotnetupOnlyComment(ArgumentName)}
                {pathExport}
                """;
        }

        return
            $"""
            {ShellProviderHelpers.GetEnvironmentConfigurationComment(ArgumentName, dotnetInstallPath)}

            set -gx DOTNET_ROOT '{escapedPath}'
            {pathExport}
            """;
    }

    public IReadOnlyList<string> GetProfilePaths()
    {
        var configurationDirectory = ShellProviderHelpers.GetFishConfigurationDirectoryOrThrow();
        return [Path.Combine(configurationDirectory, "dotnetup.fish")];
    }

    public string GenerateProfileEntry(string dotnetupPath, bool includeDotnet = true, bool includeDotnetup = true, string? dotnetInstallPath = null)
    {
        var flags = ShellProviderHelpers.GetCommandFlags(includeDotnet, includeDotnetup, dotnetInstallPath, ShellProviderHelpers.EscapeFishPath);
        return ShellProviderHelpers.BuildFishProfileEntry(dotnetupPath, ArgumentName, flags);
    }

    public string GenerateActivationCommand(string dotnetupPath)
        => ShellProviderHelpers.BuildFishActivationCommand(dotnetupPath);
}
