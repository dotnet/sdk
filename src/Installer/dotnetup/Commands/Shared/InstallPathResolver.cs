// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

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
    /// Result of install path resolution containing the resolved path and any path from global.json.
    /// </summary>
    public record InstallPathResolutionResult(
        string ResolvedInstallPath,
        string? InstallPathFromGlobalJson);

    /// <summary>
    /// Resolves the install path using the following precedence:
    /// 1. Path from global.json (if available)
    /// 2. Explicitly provided install path
    /// 3. Current user installation path (if exists)
    /// 4. Interactive prompt (if interactive mode)
    /// 5. Default install path
    /// </summary>
    /// <param name="explicitInstallPath">The install path explicitly provided by the user (e.g., --install-path option).</param>
    /// <param name="globalJsonInfo">Information from global.json, if available.</param>
    /// <param name="currentDotnetInstallRoot">Current .NET installation configuration, if any.</param>
    /// <param name="interactive">Whether to prompt the user for input.</param>
    /// <param name="componentDescription">Description of the component being installed (e.g., ".NET SDK", ".NET Runtime").</param>
    /// <param name="error">Output parameter for any error message.</param>
    /// <returns>The resolution result, or null if an error occurred.</returns>
    public InstallPathResolutionResult? Resolve(
        string? explicitInstallPath,
        GlobalJsonInfo? globalJsonInfo,
        DotnetInstallRootConfiguration? currentDotnetInstallRoot,
        bool interactive,
        string componentDescription,
        out string? error)
    {
        error = null;
        string? resolvedInstallPath = null;
        string? installPathFromGlobalJson = null;

        if (globalJsonInfo?.GlobalJsonPath is not null)
        {
            installPathFromGlobalJson = globalJsonInfo.SdkPath;

            if (installPathFromGlobalJson is not null && explicitInstallPath is not null &&
                !DotnetupUtilities.PathsEqual(installPathFromGlobalJson, explicitInstallPath))
            {
                //  TODO: Add parameter to override error
                error = $"Error: The install path specified in global.json ({installPathFromGlobalJson}) does not match the install path provided ({explicitInstallPath}).";
                return null;
            }

            resolvedInstallPath = installPathFromGlobalJson;
        }

        if (resolvedInstallPath == null)
        {
            resolvedInstallPath = explicitInstallPath;
        }

        if (resolvedInstallPath == null && currentDotnetInstallRoot is not null && currentDotnetInstallRoot.InstallType == InstallType.User)
        {
            //  If a user installation is already set up, we don't need to prompt for the install path
            resolvedInstallPath = currentDotnetInstallRoot.Path;
        }

        if (resolvedInstallPath == null)
        {
            if (interactive)
            {
                resolvedInstallPath = SpectreAnsiConsole.Prompt(
                    new TextPrompt<string>($"Where should we install the {componentDescription} to?)")
                        .DefaultValue(_dotnetInstaller.GetDefaultDotnetInstallPath()));
            }
            else
            {
                //  If no install path is specified, use the default install path
                resolvedInstallPath = _dotnetInstaller.GetDefaultDotnetInstallPath();
            }
        }

        return new InstallPathResolutionResult(resolvedInstallPath, installPathFromGlobalJson);
    }
}
