// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Shell;

public class PowerShellEnvShellProvider : IEnvShellProvider
{
    public string ArgumentName => "pwsh";

    public string Extension => "ps1";

    public string? HelpDescription => "PowerShell Core (pwsh)";

    public override string ToString() => ArgumentName;

    public string GenerateEnvScript(string dotnetInstallPath, string dotnetupDir = "", bool includeDotnet = true)
    {
        var escapedPath = ShellProviderHelpers.EscapePowerShellPath(dotnetInstallPath);
        var pathExport = ShellProviderHelpers.BuildPowerShellPathExport(escapedPath, dotnetupDir, includeDotnet);

        if (!includeDotnet)
        {
            return
                $"""
                {ShellProviderHelpers.GetDotnetupOnlyComment()}
                {pathExport}
                """;
        }

        return
            $"""
            {ShellProviderHelpers.GetEnvironmentConfigurationComment(dotnetInstallPath)}

            $env:DOTNET_ROOT = '{escapedPath}'
            {pathExport}
            """;
    }

    public IReadOnlyList<string> GetProfilePaths()
    {
        var profileDir = ShellProviderHelpers.GetPowerShellProfileDirectoryOrThrow();
        return [Path.Combine(profileDir, "Microsoft.PowerShell_profile.ps1")];
    }

    public string GenerateProfileEntry(string dotnetupPath, bool dotnetupOnly = false, string? dotnetInstallPath = null)
    {
        var flags = ShellProviderHelpers.GetCommandFlags(dotnetupOnly, dotnetInstallPath, ShellProviderHelpers.EscapePowerShellPath);
        return ShellProviderHelpers.BuildPowerShellProfileEntry(dotnetupPath, "pwsh", flags);
    }

    public string GenerateActivationCommand(string dotnetupPath, bool dotnetupOnly = false, string? dotnetInstallPath = null)
    {
        var escapedPath = ShellProviderHelpers.EscapePowerShellPath(dotnetupPath);
        var flags = ShellProviderHelpers.GetCommandFlags(dotnetupOnly, dotnetInstallPath, ShellProviderHelpers.EscapePowerShellPath);
        var command = ShellProviderHelpers.AppendArguments($"& '{escapedPath}' print-env-script --shell pwsh", flags);
        return $"{command} | Invoke-Expression";
    }
}
