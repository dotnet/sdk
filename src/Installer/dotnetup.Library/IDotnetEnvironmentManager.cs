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

    DotnetInstallRootConfiguration? GetCurrentPathConfiguration();

    string? GetLatestInstalledSystemVersion();

    List<string> GetInstalledSystemSdkVersions();

    List<DotnetInstall> GetExistingSystemInstalls();

    void ApplyEnvironmentModifications(InstallType installType, string? dotnetRoot = null);

    void ApplyTerminalProfileModifications(string dotnetRoot, IEnvShellProvider? shellProvider = null);

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
    InstallType InstallType,
    bool IsFullyConfigured)
{
    public string Path => InstallRoot.Path;
}
