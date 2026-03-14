// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Resolves the installation path for .NET components based on various inputs including
/// global.json, explicit paths, current installations, and user prompts.
/// </summary>
internal class InstallPathResolver
{
    private readonly IDotnetInstallManager _dotnetInstaller;

    public InstallPathResolver(IDotnetInstallManager dotnetInstaller)
    {
        _dotnetInstaller = dotnetInstaller;
    }

    /// <summary>
    /// Result of install path resolution containing the resolved path, any path from global.json,
    /// and how the path was determined (for telemetry).
    /// </summary>
    /// <param name="ResolvedInstallPath">The final resolved install path.</param>
    /// <param name="InstallPathFromGlobalJson">The install path from global.json, if any.</param>
    /// <param name="PathSource">How the path was determined.</param>
    public record InstallPathResolutionResult(
        string ResolvedInstallPath,
        string? InstallPathFromGlobalJson,
        PathSource PathSource);

    /// <summary>
    /// Resolves the install path using the following precedence:
    /// 1. Path from global.json (if available)
    /// 2. Explicitly provided install path
    /// 3. Current user installation path (if exists)
    /// 4. Default install path
    /// </summary>
    /// <param name="explicitInstallPath">The install path explicitly provided by the user (e.g., --install-path option).</param>
    /// <param name="globalJsonInfo">Information from global.json, if available.</param>
    /// <param name="currentDotnetInstallRoot">Current .NET installation configuration, if any.</param>
    /// <param name="error">Output parameter for any error message.</param>
    /// <returns>The resolution result, or null if an error occurred.</returns>
    public InstallPathResolutionResult? Resolve(
        string? explicitInstallPath,
        GlobalJsonInfo? globalJsonInfo,
        DotnetInstallRootConfiguration? currentDotnetInstallRoot,
        out string? error)
    {
        error = null;
        string? installPathFromGlobalJson = globalJsonInfo?.GlobalJsonPath is not null
            ? globalJsonInfo.SdkPath
            : null;

        // Resolution precedence:
        // 1. Explicit --install-path always wins
        // 2. global.json sdk-path
        // 3. Existing user installation
        // 4. Default install path

        if (explicitInstallPath is not null)
        {
            return new InstallPathResolutionResult(explicitInstallPath, installPathFromGlobalJson, PathSource.Explicit);
        }
        else if (installPathFromGlobalJson is not null)
        {
            return new InstallPathResolutionResult(installPathFromGlobalJson, installPathFromGlobalJson, PathSource.GlobalJson);
        }
        else if (currentDotnetInstallRoot is not null && currentDotnetInstallRoot.InstallType == InstallType.User)
        {
            return new InstallPathResolutionResult(currentDotnetInstallRoot.Path, installPathFromGlobalJson, PathSource.ExistingUserInstall);
        }
        else
        {
            return new InstallPathResolutionResult(_dotnetInstaller.GetDefaultDotnetInstallPath(), installPathFromGlobalJson, PathSource.Default);
        }
    }
}
