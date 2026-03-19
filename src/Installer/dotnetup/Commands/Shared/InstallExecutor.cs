// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;
using OrchestratorInstallResult = Microsoft.DotNet.Tools.Bootstrapper.InstallResult;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Handles the execution of .NET component installations with consistent messaging and progress handling.
/// </summary>
internal class InstallExecutor
{
    // ── Shared format strings for install result messages ──
    private const string InstalledAtFormat = "Installed {0} at [{1}]{2}[/]";
    private const string AlreadyInstalledAtFormat = "{0} was already installed at [{1}]{2}[/]";

    /// <summary>
    /// Result of an installation execution.
    /// </summary>
    public record InstallResult(DotnetInstall Install, bool WasAlreadyInstalled = false);

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
    /// <param name="installSource">The source of this install request.</param>
    /// <param name="globalJsonPath">The path to the global.json that triggered this install.</param>
    /// <param name="untracked">If true, install without recording in the manifest.</param>
    /// <param name="verbosity">The verbosity level for diagnostic messages during installation.</param>
    /// <returns>The resolved install request with version information.</returns>
    public static ResolvedInstallRequest CreateAndResolveRequest(
        string installPath,
        string channel,
        InstallComponent component,
        string? manifestPath,
        ChannelVersionResolver channelVersionResolver,
        bool requireMuxerUpdate = false,
        InstallRequestSource installSource = InstallRequestSource.Explicit,
        string? globalJsonPath = null,
        bool untracked = false,
        Verbosity verbosity = Verbosity.Normal)
    {
        var installRoot = new DotnetInstallRoot(installPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var request = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel(channel),
            component,
            new InstallRequestOptions
            {
                ManifestPath = manifestPath,
                RequireMuxerUpdate = requireMuxerUpdate,
                InstallSource = installSource,
                GlobalJsonPath = globalJsonPath,
                Untracked = untracked,
                Verbosity = verbosity
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
        SpectreAnsiConsole.MarkupLine(string.Format(
            CultureInfo.InvariantCulture,
            "Installing {0} [{1}]{2}[/] to [{1}]{3}[/]...",
            componentDescription,
            DotnetupTheme.Current.Accent,
            resolvedVersion,
            installRequest.InstallRoot.Path.EscapeMarkup()));

        var orchestratorResult = InstallerOrchestratorSingleton.Instance.Install(installRequest, noProgress);

        string successAccent = DotnetupTheme.Current.SuccessAccent;
        string version = orchestratorResult.Install.Version.ToString().EscapeMarkup();
        string path = orchestratorResult.Install.InstallRoot.Path.EscapeMarkup();
        string label = string.Format(CultureInfo.InvariantCulture, "{0} [{1}]{2}[/]", componentDescription, successAccent, version);

        if (orchestratorResult.WasAlreadyInstalled)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, AlreadyInstalledAtFormat, label, successAccent, path));
        }
        else
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, InstalledAtFormat, label, successAccent, path));
        }

        return new InstallResult(orchestratorResult.Install, orchestratorResult.WasAlreadyInstalled);
    }

    /// <summary>
    /// Executes the installation of additional admin installs (SDKs and runtimes), optionally including
    /// a primary install request. Uses a two-phase approach: SDKs are installed first so that
    /// runtimes bundled within SDK archives are already on disk, then only standalone runtimes
    /// that are not yet present are downloaded.
    /// </summary>
    /// <param name="additionalInstalls">The list of additional admin installs to migrate.</param>
    /// <param name="installRoot">The installation root path.</param>
    /// <param name="manifestPath">Optional manifest path.</param>
    /// <param name="noProgress">Whether to suppress progress display.</param>
    /// <param name="requireMuxerUpdate">If true, fail when the muxer cannot be updated.</param>
    /// <param name="primaryRequest">Optional primary install request to batch with the appropriate phase.</param>
    /// <returns>The primary install result when <paramref name="primaryRequest"/> is supplied; null otherwise.</returns>
    public static InstallResult[] ExecuteAdditionalInstalls(
        IEnumerable<DotnetInstall> additionalInstalls,
        DotnetInstallRoot installRoot,
        string? manifestPath,
        bool noProgress,
        bool requireMuxerUpdate = false,
        DotnetInstallRequest? primaryRequest = null)
    {
        var installsList = additionalInstalls.ToList();
        if (installsList.Count == 0 && primaryRequest is null)
        {
            return Array.Empty<InstallResult>();
        }

        var sdkInstalls = installsList.Where(i => i.Component == InstallComponent.SDK).ToList();
        var runtimeInstalls = installsList.Where(i => i.Component != InstallComponent.SDK).ToList();

        bool primaryIsSdk = primaryRequest is not null && primaryRequest.Component == InstallComponent.SDK;
        var sdkPrimary = primaryIsSdk ? primaryRequest : null;
        var runtimePrimary = primaryIsSdk ? null : primaryRequest;

        DisplayBatchSummary(primaryRequest, installsList, installRoot);

        // Phase 1: install SDKs first — their archives typically bundle runtime binaries.
        var sdkResults = RunInstallBatch(sdkInstalls, installRoot, manifestPath, noProgress, requireMuxerUpdate, sdkPrimary);

        // Phase 2: skip runtimes whose files already landed on disk via an SDK archive.
        var remainingRuntimes = runtimeInstalls.Where(r => !RuntimeFolderExistsOnDisk(installRoot, r)).ToList();
        var runtimeResults = RunInstallBatch(remainingRuntimes, installRoot, manifestPath, noProgress, requireMuxerUpdate, runtimePrimary);

        return [sdkResults, runtimeResults];
    }

    private static void DisplayBatchSummary(
        DotnetInstallRequest? primaryRequest,
        List<DotnetInstall> additionalInstalls,
        DotnetInstallRoot installRoot)
    {
        if (primaryRequest is null)
        {
            return;
        }

        string desc = primaryRequest.Component.GetDisplayName();
        SpectreAnsiConsole.MarkupLine(string.Format(
            CultureInfo.InvariantCulture,
            "Installing {0} [{1}]{2}[/] and {3} additional component(s) to [{1}]{4}[/]...",
            desc,
            DotnetupTheme.Current.Accent,
            primaryRequest.ResolvedVersion,
            additionalInstalls.Count,
            installRoot.Path.EscapeMarkup()));
    }

    private static InstallResult[] RunInstallBatch(
        List<DotnetInstall> installs,
        DotnetInstallRoot installRoot,
        string? manifestPath,
        bool noProgress,
        bool requireMuxerUpdate,
        DotnetInstallRequest? primaryRequest)
    {
        var requests = BuildBatchRequestsFromInstalls(primaryRequest, installs, installRoot, manifestPath, requireMuxerUpdate);
        if (requests.Count == 0)
        {
            return Array.Empty<InstallResult>();
        }

        IReadOnlyList<OrchestratorInstallResult> results;

        // Scope the progress reporter so the progress bar finishes rendering
        // before we print the result summary lines beneath it.
        {
            IProgressTarget progressTarget = noProgress ? new NonUpdatingProgressTarget() : new SpectreProgressTarget();
            using var sharedReporter = new LazyProgressReporter(progressTarget);
            results = InstallerOrchestratorSingleton.Instance.InstallMany(requests, sharedReporter);
        }

        DisplayBatchResults(results, primaryRequest);
        return results.ToArray();
    }

    private static bool RuntimeFolderExistsOnDisk(DotnetInstallRoot installRoot, DotnetInstall runtime)
    {
        string frameworkDir = Path.Combine(
            installRoot.Path,
            "shared",
            runtime.Component.GetFrameworkName(),
            runtime.Version.ToString());
        return Directory.Exists(frameworkDir);
    }

    private static List<DotnetInstallRequest> BuildBatchRequestsFromInstalls(
        DotnetInstallRequest? primaryRequest,
        List<DotnetInstall> additionalInstalls,
        DotnetInstallRoot installRoot,
        string? manifestPath,
        bool requireMuxerUpdate)
    {
        var requests = new List<DotnetInstallRequest>();
        // Track (Component, VersionString) pairs already included so duplicates are skipped.
        var seen = new HashSet<(InstallComponent, string)>();

        if (primaryRequest is not null)
        {
            requests.Add(primaryRequest);
            string primaryVersion = primaryRequest.ResolvedVersion?.ToString()
                ?? primaryRequest.Channel.ToString() ?? string.Empty;
            seen.Add((primaryRequest.Component, primaryVersion));
        }

        foreach (var install in additionalInstalls)
        {
            if (!seen.Add((install.Component, install.Version.ToString())))
            {
                continue;
            }

            requests.Add(new DotnetInstallRequest(
                installRoot,
                new UpdateChannel(install.Version.ToString()),
                install.Component,
                new InstallRequestOptions
                {
                    ManifestPath = manifestPath,
                    RequireMuxerUpdate = requireMuxerUpdate
                }));
        }

        return requests;
    }

    private static InstallResult? DisplayBatchResults(
        IReadOnlyList<OrchestratorInstallResult> results,
        DotnetInstallRequest? primaryRequest)
    {
        InstallResult? primaryResult = null;
        var installed = new List<string>();
        var alreadyInstalled = new List<string>();
        string? sharedPath = null;

        foreach (var result in results)
        {
            bool isPrimary = primaryRequest is not null
                && result.Install.Version.ToString() == primaryRequest.ResolvedVersion?.ToString()
                && result.Install.Component == primaryRequest.Component;

            if (isPrimary && primaryResult is null)
            {
                primaryResult = new InstallResult(result.Install, result.WasAlreadyInstalled);
            }

            sharedPath ??= result.Install.InstallRoot.Path;
            string successAccent = DotnetupTheme.Current.SuccessAccent;
            string installDetailLine = string.Format(CultureInfo.InvariantCulture, "{0} [{1}]{2}[/]", result.Install.Component.GetDisplayName(), successAccent, result.Install.Version.ToString().EscapeMarkup());
            if (result.WasAlreadyInstalled)
            {
                alreadyInstalled.Add(installDetailLine);
            }
            else
            {
                installed.Add(installDetailLine);
            }
        }

        EmitBatchSummaryLines(installed, alreadyInstalled, sharedPath);
        return primaryResult;
    }

    private static void EmitBatchSummaryLines(List<string> installed, List<string> alreadyInstalled, string? sharedPath)
    {
        string successAccent = DotnetupTheme.Current.SuccessAccent;
        string escapedPath = sharedPath?.EscapeMarkup() ?? string.Empty;

        if (installed.Count > 0)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture,
                "Installed at [{0}]{1}[/]:", successAccent, escapedPath));
            foreach (var item in installed)
            {
                SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0}", item));
            }
        }

        if (alreadyInstalled.Count > 0)
        {
            SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture,
                "Already installed at [{0}]{1}[/]:", successAccent, escapedPath));
            foreach (var item in alreadyInstalled)
            {
                SpectreAnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0}", item));
            }
        }
    }

    /// <summary>
    /// Resolves the path preference and derives whether to set a default install.
    /// When <paramref name="installPath"/> is explicitly provided the user already knows
    /// where to install, so the path-preference walkthrough is skipped.
    /// Shared by SDK and Runtime install paths.
    /// </summary>
    public static (PathPreference? PathPreference, bool? ReplaceSystemInstallConfiguration) ResolveInstallDefaults(bool interactive, bool? replaceSystemConfig, string? installPath)
    {
        // Explicit install path: skip path-preference resolution entirely.
        if (installPath is not null)
        {
            return (null, replaceSystemConfig);
        }

        var pathPreference = DotnetupConfig.EnsurePathPreference(interactive);
        bool? replaceSystemEnvironment = replaceSystemConfig ?? (pathPreference == PathPreference.FullPathReplacement ? true : null);
        return (pathPreference, replaceSystemEnvironment);
    }

    /// <summary>
    /// Runs a multi-install batch: creates progress reporting, calls InstallMany,
    /// displays results, and optionally configures the default install type.
    /// Shared by SDK and Runtime multi-install paths.
    /// </summary>
    public static void RunMultiInstall(
        List<DotnetInstallRequest> requests,
        string installPath,
        bool noProgress,
        bool? setDefaultInstall,
        IDotnetInstallManager dotnetInstaller)
    {
        IReadOnlyList<OrchestratorInstallResult> results;

        {
            IProgressTarget progressTarget = noProgress ? new NonUpdatingProgressTarget() : new SpectreProgressTarget();
            using var sharedReporter = new LazyProgressReporter(progressTarget);
            results = InstallerOrchestratorSingleton.Instance.InstallMany(requests, sharedReporter);
        }

        DisplayBatchResults(results, null);

        if (setDefaultInstall == true)
        {
            dotnetInstaller.ConfigureInstallType(InstallType.User, installPath);
        }
    }
}
