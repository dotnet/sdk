// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper;

//  Install process
// - Resolve version to install from channel
// - Handle writing to install manifest and garbage collection
// - Orchestrate installation so that only one install happens at a time
// - Call into installer implementation

internal interface IDotnetEnvironmentManager
{
    string GetDefaultDotnetInstallPath();

    /// <summary>
    /// Resolves the <c>dotnet</c> that currently wins on <c>PATH</c> and reports whether it is a
    /// dotnetup-managed hive (i.e. an install dotnetup owns and may run or uninstall from).
    /// Returns <c>null</c> when no <c>dotnet</c> is found on <c>PATH</c>.
    /// </summary>
    DotnetInstallRootConfiguration? GetCurrentPathConfiguration();

    string? GetLatestInstalledSystemVersion();

    List<string> GetInstalledSystemSdkVersions();

    List<DotnetInstall> GetExistingSystemInstalls();

    /// <summary>
    /// Applies machine/user-level environment configuration (PATH and DOTNET_ROOT environment
    /// variables) to point to either the system (Program Files) or user dotnet install location.
    /// </summary>
    void ApplyEnvironmentModifications(InstallType installType, string? dotnetRoot = null);

    /// <summary>
    /// Writes (or rewrites) dotnetup's managed block in the current user's shell profile.
    /// The two aspects are independent: <paramref name="includeDotnet"/> wires the managed
    /// dotnet (DOTNET_ROOT + dotnet on PATH); <paramref name="includeDotnetup"/> adds the
    /// dotnetup directory to PATH. At least one should be true (to remove the block entirely,
    /// use <see cref="Shell.ShellProfileManager.RemoveProfileEntries"/>).
    /// </summary>
    void ApplyTerminalProfileModifications(string dotnetRoot, bool includeDotnet = true, bool includeDotnetup = true, IEnvShellProvider? shellProvider = null);

    /// <summary>
    /// Ensures the dotnetup directory is present (<paramref name="enabled"/> == true) or absent
    /// (false) on the user-scope <c>PATH</c> environment variable. Windows-only; a no-op on other
    /// platforms, where dotnetup-on-PATH is handled entirely by the shell profile block. This is
    /// what lets cmd.exe and GUI-launched apps — which read no shell profile — find dotnetup.
    /// </summary>
    void ApplyDotnetupOnUserPath(bool enabled);

    /// <summary>
    /// Updates the global.json file to reflect the installed SDK version,
    /// if a global.json exists and the install was global.json-sourced.
    /// </summary>
    void ApplyGlobalJsonModifications(IReadOnlyList<ResolvedInstallRequest> requests);
}

public class GlobalJsonInfo
{
    public string? GlobalJsonPath { get; set; }
    public GlobalJsonContents? GlobalJsonContents { get; set; }

    // Convenience properties for compatibility
    public string? SdkVersion => GlobalJsonContents?.Sdk?.Version;
    public bool? AllowPrerelease => GlobalJsonContents?.Sdk?.AllowPrerelease;
    public string? RollForward => GlobalJsonContents?.Sdk?.RollForward;
    public string? SdkPath
    {
        get
        {
            return (GlobalJsonContents?.Sdk?.Paths is not null && GlobalJsonContents.Sdk.Paths.Length > 0) ?
                Path.GetFullPath(GlobalJsonContents.Sdk.Paths[0], Path.GetDirectoryName(GlobalJsonPath)!) : null;
        }
    }
}

public record DotnetInstallRootConfiguration(
    DotnetInstallRoot InstallRoot,
    bool IsDotnetupHive)
{
    public string Path => InstallRoot.Path;
}
