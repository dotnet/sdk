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
    /// <summary>
    /// The "$host$" sentinel in <c>sdk.paths</c> means "use the default host location"
    /// rather than a literal directory. See
    /// https://learn.microsoft.com/en-us/dotnet/core/tools/global-json#paths.
    /// </summary>
    private const string HostLocationSentinel = "$host$";

    /// <summary>
    /// The first meaningful (non-null, non-whitespace) entry in <c>sdk.paths</c>, or
    /// <see langword="null"/> when there are none. <c>sdk.paths</c> is an ordered list, so
    /// dotnetup honors the first meaningful entry as the desired install location, mirroring
    /// how the .NET host resolver consumes the list.
    /// </summary>
    private string? FirstSdkPathEntry =>
        GlobalJsonContents?.Sdk?.Paths is { Length: > 0 } paths
            ? Array.Find(paths, p => !string.IsNullOrWhiteSpace(p))
            : null;

    /// <summary>
    /// <see langword="true"/> when the first meaningful <c>sdk.paths</c> entry is the
    /// "$host$" sentinel, meaning the SDK should resolve to (and be installed at) the default
    /// host location instead of a literal path. In that case <see cref="SdkPath"/> is
    /// <see langword="null"/> and the caller substitutes the default install location.
    /// </summary>
    public bool UsesDefaultHostLocation =>
        string.Equals(FirstSdkPathEntry, HostLocationSentinel, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The literal install path declared by <c>sdk.paths</c>, resolved relative to the
    /// directory containing global.json. Returns <see langword="null"/> when there is no usable
    /// path, or when the first meaningful entry is the "$host$" sentinel (in which case
    /// <see cref="UsesDefaultHostLocation"/> is <see langword="true"/>).
    /// </summary>
    public string? SdkPath
    {
        get
        {
            var firstEntry = FirstSdkPathEntry;
            if (firstEntry is null || UsesDefaultHostLocation)
            {
                return null;
            }

            return Path.GetFullPath(firstEntry, Path.GetDirectoryName(GlobalJsonPath)!);
        }
    }
}

public record DotnetInstallRootConfiguration(
    DotnetInstallRoot InstallRoot,
    bool IsDotnetupHive)
{
    public string Path => InstallRoot.Path;
}
