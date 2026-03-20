// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Walkthrough;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Shared installation workflow that handles the common installation logic for SDK and Runtime commands.
/// Generates install requests, validates paths, and either executes directly or delegates to the
/// walkthrough for environment configuration.
/// </summary>
internal class InstallWorkflow
{
    private readonly InstallCommand _command;
    private readonly InstallPathResolver _installPathResolver;

    public InstallWorkflow(InstallCommand command)
    {
        _command = command;
        _installPathResolver = new InstallPathResolver(command.DotnetInstaller);
    }

    /// <summary>
    /// Executes the install workflow for the given channels/versions and component type.
    /// When an explicit install path is provided, installs directly.
    /// When interactive and no explicit path, wraps execution in the walkthrough
    /// for environment configuration (path preference, admin migration, etc.).
    /// Otherwise, installs to the default/resolved path without prompting.
    /// </summary>
    public void Execute(string[] versionsOrChannels, InstallComponent component)
    {
        var requests = GenerateInstallRequests(versionsOrChannels, component);

        if (_command.InstallPath is not null || !_command.Interactive)
        {
            // Explicit path or non-interactive — skip walkthrough entirely
            ExecuteInstallRequests(requests);
        }
        else
        {
            // Interactive with no explicit path — walkthrough for path preference, admin migration, etc.
            var workflows = new WalkthroughWorkflows(_command.DotnetInstaller, _command.ChannelVersionResolver);
            workflows.BaseConfigurationWalkthrough(
                requests,
                () => ExecuteInstallRequests(requests),
                _command.NoProgress);
        }
    }

    /// <summary>
    /// Generates resolved install requests for the given channels/versions.
    /// Handles path resolution, global.json channel inference (for SDK), install path validation,
    /// and version resolution via the channel version resolver.
    /// </summary>
    public List<ResolvedInstallRequest> GenerateInstallRequests(
        string[] versionsOrChannels,
        InstallComponent component)
    {
        var globalJson = GlobalJsonModifier.GetGlobalJsonInfo(Environment.CurrentDirectory);
        var currentInstallRoot = _command.DotnetInstaller.GetCurrentPathConfiguration();

        var pathResolution = _installPathResolver.Resolve(
            _command.InstallPath,
            globalJson,
            currentInstallRoot);

        ValidateInstallPath(pathResolution.ResolvedInstallPath, pathResolution.PathSource, _command.ManifestPath);

        // Resolve channels: if none provided, try global.json (SDK only), then default to "latest"
        string[] resolvedChannels;
        if (versionsOrChannels.Length > 0)
        {
            resolvedChannels = versionsOrChannels;
        }
        else
        {
            string? channelFromGlobalJson = null;
            if (component == InstallComponent.SDK && globalJson?.GlobalJsonPath is not null)
            {
                channelFromGlobalJson = GlobalJsonChannelResolver.ResolveChannel(globalJson.GlobalJsonPath);
            }

            if (channelFromGlobalJson is not null)
            {
                SpectreAnsiConsole.MarkupLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "[{0}]{1} {2} will be installed since {3} specifies that version.[/]",
                    DotnetupTheme.Current.Dim,
                    component.GetDisplayName(),
                    channelFromGlobalJson,
                    globalJson!.GlobalJsonPath!));
                resolvedChannels = [channelFromGlobalJson];
            }
            else
            {
                resolvedChannels = [ChannelVersionResolver.LatestChannel];
            }
        }

        var installRoot = new DotnetInstallRoot(
            pathResolution.ResolvedInstallPath,
            InstallerUtilities.GetDefaultInstallArchitecture());

        var requests = new List<ResolvedInstallRequest>();
        foreach (string channel in resolvedChannels)
        {
            // Determine install source for manifest tracking
            bool isFromGlobalJson = versionsOrChannels.Length == 0
                && globalJson?.GlobalJsonPath is not null
                && component == InstallComponent.SDK;
            var installSource = isFromGlobalJson
                ? InstallRequestSource.GlobalJson
                : InstallRequestSource.Explicit;

            var request = new DotnetInstallRequest(
                installRoot,
                new UpdateChannel(channel),
                component,
                new InstallRequestOptions
                {
                    ManifestPath = _command.ManifestPath,
                    RequireMuxerUpdate = _command.RequireMuxerUpdate,
                    InstallSource = installSource,
                    GlobalJsonPath = isFromGlobalJson ? globalJson?.GlobalJsonPath : null,
                    Untracked = _command.Untracked,
                    Verbosity = _command.Verbosity
                });

            var resolvedVersion = _command.ChannelVersionResolver.Resolve(request);

            if (resolvedVersion is null)
            {
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.VersionNotFound,
                    $"Could not resolve channel '{channel}' to a .NET version for {component.GetDisplayName()}.");
            }

            var resolved = new ResolvedInstallRequest(request, resolvedVersion);

            RecordInstallTelemetry(
                component, versionsOrChannels.Length > 0 ? channel : null,
                _command.InstallPath, globalJson, currentInstallRoot,
                pathResolution, channel, resolved);

            requests.Add(resolved);
        }

        return requests;
    }

    /// <summary>
    /// Executes resolved install requests via the install executor as a concurrent batch.
    /// </summary>
    private void ExecuteInstallRequests(List<ResolvedInstallRequest> requests)
    {
        var results = InstallExecutor.ExecuteInstalls(requests, _command.NoProgress);

        foreach (var result in results)
        {
            Activity.Current?.SetTag(TelemetryTagNames.InstallResult, result.WasAlreadyInstalled ? "already_installed" : "installed");
        }
    }

    private static void ValidateInstallPath(string installPath, PathSource pathSource, string? manifestPath)
    {
        // Block install paths that point to existing files (not directories)
        if (File.Exists(installPath))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.InstallPathIsFile,
                $"The install path '{installPath}' is an existing file, not a directory. " +
                "Please specify a directory path for the installation.");
        }

        // Block admin/system-managed install paths — dotnetup should not install there
        if (InstallPathClassifier.IsAdminInstallPath(installPath))
        {
            Activity.Current?.SetTag(TelemetryTagNames.InstallPathType, "admin");
            Activity.Current?.SetTag(TelemetryTagNames.InstallPathSource, pathSource.ToString().ToLowerInvariant());
            throw new DotnetInstallException(
                DotnetInstallErrorCode.AdminPathBlocked,
                $"The install path '{installPath}' is a system-managed .NET location. " +
                "dotnetup cannot install to the default system .NET directory (Program Files\\dotnet on Windows, /usr/share/dotnet on Linux/macOS). " +
                "Use your system package manager or the official installer for system-wide installations, or choose a different path.");
        }

        // Check for untracked .NET artifacts at the install path
        var installRoot = new DotnetInstallRoot(installPath, InstallerUtilities.GetDefaultInstallArchitecture());
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifestData = new DotnetupSharedManifest(manifestPath).ReadManifest();
        if (!InstallerOrchestratorSingleton.IsRootInManifest(manifestData, installRoot)
            && InstallerOrchestratorSingleton.HasDotnetArtifacts(installRoot.Path))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.Unknown,
                $"The install path '{installRoot.Path}' already contains a .NET installation that is not tracked by dotnetup. " +
                "To avoid conflicts, use a different install path or remove the existing installation first.");
        }
    }

    private static void RecordInstallTelemetry(
        InstallComponent component,
        string? requestedVersionOrChannel,
        string? explicitInstallPath,
        GlobalJsonInfo? globalJson,
        DotnetInstallRootConfiguration? currentInstallRoot,
        InstallPathResolver.InstallPathResolutionResult pathResolution,
        string resolvedChannel,
        ResolvedInstallRequest resolved)
    {
        // Request-level tags
        Activity.Current?.SetTag(TelemetryTagNames.InstallComponent, component.ToString());
        Activity.Current?.SetTag(TelemetryTagNames.InstallRequestedVersion, VersionSanitizer.Sanitize(requestedVersionOrChannel));
        Activity.Current?.SetTag(TelemetryTagNames.InstallPathExplicit, explicitInstallPath is not null);

        // Resolved context tags
        Activity.Current?.SetTag(TelemetryTagNames.InstallHasGlobalJson, globalJson?.GlobalJsonPath is not null);
        Activity.Current?.SetTag(TelemetryTagNames.InstallExistingInstallType, currentInstallRoot?.InstallType.ToString() ?? "none");
        Activity.Current?.SetTag(TelemetryTagNames.InstallPathType, InstallPathClassifier.ClassifyInstallPath(pathResolution.ResolvedInstallPath, pathResolution.PathSource));
        Activity.Current?.SetTag(TelemetryTagNames.InstallPathSource, pathResolution.PathSource.ToString().ToLowerInvariant());

        // Resolved version tags
        Activity.Current?.SetTag(TelemetryTagNames.InstallResolvedVersion, resolved.ResolvedVersion.ToString());

        string requestSource = requestedVersionOrChannel is not null
            ? "explicit"
            : globalJson?.GlobalJsonPath is not null
                ? "default-globaljson"
                : "default-latest";
        Activity.Current?.SetTag(TelemetryTagNames.DotnetRequestSource, requestSource);
        Activity.Current?.SetTag(TelemetryTagNames.DotnetRequested, VersionSanitizer.Sanitize(resolvedChannel));
    }
}
