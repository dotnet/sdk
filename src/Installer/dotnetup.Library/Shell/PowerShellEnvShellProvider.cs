// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Shell;

public class PowerShellEnvShellProvider : IEnvShellProvider
{
    public string ArgumentName => "pwsh";

    public string Extension => "ps1";

    public string? HelpDescription => "PowerShell Core (pwsh)";

    public override string ToString() => ArgumentName;

    // Subfolder names under the user's Documents folder that hold each flavor's profile files.
    internal const string WindowsPowerShellProfileFolder = "WindowsPowerShell";
    internal const string PowerShellCoreProfileFolder = "PowerShell";

    // CurrentUserAllHosts profile filename. Used for all flavors and on all platforms.
    internal const string ProfileFileName = "profile.ps1";

    public string GenerateEnvScript(string dotnetInstallPath, string dotnetupDir = "", bool includeDotnet = true)
    {
        var escapedPath = ShellProviderHelpers.EscapePowerShellPath(dotnetInstallPath);
        var pathExport = ShellProviderHelpers.BuildPowerShellPathExport(escapedPath, dotnetupDir, includeDotnet);

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

            $env:DOTNET_ROOT = '{escapedPath}'
            {pathExport}
            """;
    }

    public IReadOnlyList<string> GetProfilePaths()
    {
        if (OperatingSystem.IsWindows())
        {
            var documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return GetWindowsProfilePaths(documentsFolder);
        }

        var profileDir = ShellProviderHelpers.GetPowerShellProfileDirectoryOrThrow();
        return [Path.Combine(profileDir, ProfileFileName)];
    }

    // Windows PowerShell 5.1 reads .ps1 files without a BOM as the system ANSI code page,
    // so a BOM-less profile containing non-ASCII characters (e.g. an install path under a
    // username with accented or CJK characters) would be misinterpreted and likely fail
    // to invoke dotnetup. On Windows we therefore create new PowerShell profile files as
    // UTF-8 with BOM. PowerShell 7+ handles a BOM transparently, so it's safe to use the
    // same encoding for the pwsh profile location too. On non-Windows we keep the BOM-less
    // default — PowerShell 7 on Linux/macOS treats BOM-less .ps1 files as UTF-8.
    //
    // Existing profile files (regardless of where they live) keep their detected encoding;
    // see ShellProfileManager.ReadProfileFile.
    public Encoding NewFileEncoding => OperatingSystem.IsWindows()
        ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
        : new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    // Write to profile.ps1, which applies to all PowerShell hosts, rather than
    // Microsoft.PowerShell_profile.ps1, which only applies to the console host.
    //
    // We want dotnetup to be available "everywhere".  On Windows we would modify the
    // PATH if we could and that would apply to cmd also, but there's not a good way
    // to override the Admin PATH that the MSI / Program Files installers set.
    //
    // Worth noting:
    //   * Most install scripts (rustup, conda init, oh-my-posh, dotnet-install) write to the
    //     host-specific Microsoft.PowerShell_profile.ps1. Users may have learned to look there.
    //   * $PROFILE (bare) points to Microsoft.PowerShell_profile.ps1. A user
    //     running `notepad $PROFILE` won't see our entry -- they have to know to open
    //     $PROFILE.CurrentUserAllHosts.
    //   * profile.ps1 runs in non-interactive embedded hosts too. Our entry shells out
    //     (& '...\dotnetup.exe' print-env-script | Out-String) every time PowerShell starts.
    //
    // We write to both Windows PowerShell (5.1) and PowerShell 7+ profile locations
    // unconditionally, regardless of whether each flavor is currently installed. This
    // means that the user installs pwsh later, dotnet is already wired up.
    internal static IReadOnlyList<string> GetWindowsProfilePaths(string documentsFolder)
    {
        if (string.IsNullOrEmpty(documentsFolder))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ContextResolutionFailed,
                "Unable to locate the current user's Documents folder.");
        }

        return
        [
            Path.Combine(documentsFolder, WindowsPowerShellProfileFolder, ProfileFileName),
            Path.Combine(documentsFolder, PowerShellCoreProfileFolder, ProfileFileName),
        ];
    }

    public string GenerateProfileEntry(string dotnetupPath, bool dotnetupOnly = false, string? dotnetInstallPath = null)
    {
        var flags = ShellProviderHelpers.GetCommandFlags(dotnetupOnly, dotnetInstallPath, ShellProviderHelpers.EscapePowerShellPath);
        return ShellProviderHelpers.BuildPowerShellProfileEntry(dotnetupPath, "pwsh", flags);
    }

    public string GenerateActivationCommand(string dotnetupPath, bool dotnetupOnly = false, string? dotnetInstallPath = null)
    {
        var flags = ShellProviderHelpers.GetCommandFlags(dotnetupOnly, dotnetInstallPath, ShellProviderHelpers.EscapePowerShellPath);
        return ShellProviderHelpers.BuildPowerShellActivationCommand(dotnetupPath, "pwsh", flags);
    }
}
