// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Handles the execution of .NET component installations with consistent messaging and progress handling.
/// </summary>
internal class InstallExecutor
{
    /// <summary>
    /// Result of an installation execution.
    /// </summary>
    public record InstallResult(bool Success, DotnetInstall? Install);

    /// <summary>
    /// Result of creating and resolving an install request.
    /// </summary>
    public record ResolvedInstallRequest(DotnetInstallRequest Request, ReleaseVersion? ResolvedVersion);

    /// <summary>
    /// Creates a DotnetInstallRequest and resolves the version using the channel version resolver.
    /// </summary>
    /// <param name="installPath">The installation path.</param>
    /// <param name="channel">The channel or version to install.</param>
    /// <param name="component">The component type (SDK, Runtime, ASPNETCore, WindowsDesktop).</param>
    /// <param name="manifestPath">Optional manifest path for tracking installations.</param>
    /// <param name="channelVersionResolver">The resolver to use for version resolution.</param>
    /// <param name="requireMuxerUpdate">If true, fail when the muxer cannot be updated.</param>
    /// <returns>The resolved install request with version information.</returns>
    public static ResolvedInstallRequest CreateAndResolveRequest(
        string installPath,
        string channel,
        InstallComponent component,
        string? manifestPath,
        ChannelVersionResolver channelVersionResolver,
        bool requireMuxerUpdate = false)
    {
        var installRoot = new DotnetInstallRoot(installPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var request = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel(channel),
            component,
            new InstallRequestOptions
            {
                ManifestPath = manifestPath,
                RequireMuxerUpdate = requireMuxerUpdate
            });

        var resolvedVersion = channelVersionResolver.Resolve(request);

        return new ResolvedInstallRequest(request, resolvedVersion);
    }

    /// <summary>
    /// Executes the installation of a .NET component and displays appropriate status messages.
    /// </summary>
    /// <param name="installRequest">The installation request to execute.</param>
    /// <param name="resolvedVersion">The resolved version string for display purposes.</param>
    /// <param name="componentDescription">Description of the component (e.g., ".NET SDK", ".NET Runtime").</param>
    /// <param name="noProgress">Whether to suppress progress display.</param>
    /// <returns>The installation result.</returns>
    public static InstallResult ExecuteInstall(
        DotnetInstallRequest installRequest,
        string? resolvedVersion,
        string componentDescription,
        bool noProgress)
    {
        SpectreAnsiConsole.MarkupLineInterpolated($"Installing {componentDescription} [blue]{resolvedVersion}[/] to [blue]{installRequest.InstallRoot.Path}[/]...");

        var install = InstallerOrchestratorSingleton.Instance.Install(installRequest, noProgress);
        if (install == null)
        {
            SpectreAnsiConsole.MarkupLine($"[red]Failed to install {componentDescription} {resolvedVersion}[/]");
            return new InstallResult(false, null);
        }

        SpectreAnsiConsole.MarkupLine($"[green]Installed {componentDescription} {install.Version}, available via {install.InstallRoot}[/]");
        return new InstallResult(true, install);
    }

    /// <summary>
    /// Executes the installation of additional versions of a .NET component.
    /// </summary>
    /// <param name="additionalVersions">The list of additional versions to install.</param>
    /// <param name="installRoot">The installation root path.</param>
    /// <param name="component">The component type to install.</param>
    /// <param name="componentDescription">Description of the component for display.</param>
    /// <param name="manifestPath">Optional manifest path.</param>
    /// <param name="noProgress">Whether to suppress progress display.</param>
    /// <param name="requireMuxerUpdate">If true, fail when the muxer cannot be updated.</param>
    /// <returns>True if all installations succeeded, false if any failed.</returns>
    public static bool ExecuteAdditionalInstalls(
        IEnumerable<string> additionalVersions,
        DotnetInstallRoot installRoot,
        InstallComponent component,
        string componentDescription,
        string? manifestPath,
        bool noProgress,
        bool requireMuxerUpdate = false)
    {
        bool allSucceeded = true;

        foreach (var additionalVersion in additionalVersions)
        {
            var additionalRequest = new DotnetInstallRequest(
                installRoot,
                new UpdateChannel(additionalVersion),
                component,
                new InstallRequestOptions
                {
                    ManifestPath = manifestPath,
                    RequireMuxerUpdate = requireMuxerUpdate
                });

            var additionalInstall = InstallerOrchestratorSingleton.Instance.Install(additionalRequest, noProgress);
            if (additionalInstall == null)
            {
                SpectreAnsiConsole.MarkupLine($"[red]Failed to install additional {componentDescription} {additionalVersion}[/]");
                allSucceeded = false;
            }
            else
            {
                SpectreAnsiConsole.MarkupLine($"[green]Installed additional {componentDescription} {additionalInstall.Version}, available via {additionalInstall.InstallRoot}[/]");
            }
        }

        return allSucceeded;
    }

    /// <summary>
    /// Configures the default .NET installation if requested.
    /// </summary>
    /// <param name="dotnetInstaller">The install manager.</param>
    /// <param name="setDefaultInstall">Whether to set as default install.</param>
    /// <param name="installPath">The installation path.</param>
    public static void ConfigureDefaultInstallIfRequested(
        IDotnetInstallManager dotnetInstaller,
        bool setDefaultInstall,
        string installPath)
    {
        if (setDefaultInstall)
        {
            dotnetInstaller.ConfigureInstallType(InstallType.User, installPath);
        }
    }

    /// <summary>
    /// Displays completion message.
    /// </summary>
    public static void DisplayComplete()
    {
        SpectreAnsiConsole.WriteLine("Complete!");
    }
}
