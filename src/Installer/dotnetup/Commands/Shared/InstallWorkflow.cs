// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Runtime.ExceptionServices;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Shared installation workflow that handles the common installation logic for SDK and Runtime commands.
/// Generates install requests, validates paths, and either executes directly or delegates to the
/// init flow for environment configuration.
/// </summary>
internal class InstallWorkflow
{
    private readonly InstallCommand _command;
    private readonly InstallPathResolver _installPathResolver;

    public InstallWorkflow(InstallCommand command)
    {
        _command = command;
        _installPathResolver = new InstallPathResolver(command.DotnetEnvironment);
    }

    /// <summary>
    /// Executes the install workflow for the given component specifications.
    /// Each spec is a (component, channel) pair where channel may be null (defaults to global.json or "latest").
    /// When an explicit install path is provided, installs directly.
    /// When interactive and no explicit path, wraps execution in the init flow
    /// for environment configuration (path preference, admin migration, etc.).
    /// Otherwise, installs to the default/resolved path without prompting.
    /// </summary>
    public void Execute(MinimalInstallSpec[] componentSpecs)
    {
        var requests = GenerateInstallRequests(componentSpecs);

        if (_command.InstallPath is not null || !_command.Interactive)
        {
            // Explicit path or non-interactive — skip the init flow entirely
            ExecuteInstallRequests(requests);
        }
        else
        {
            // Interactive with no explicit path — init flow for path preference, admin migration, etc.
            var workflows = new InitWorkflows(_command.DotnetEnvironment, _command.ChannelVersionResolver);
            workflows.BaseConfigurationWalkthrough(
                requests,
                () => ExecuteInstallRequests(requests),
                _command.NoProgress,
                _command.Interactive,
                true,
                false,
                _command.ShellProvider);
        }

        // Global.json update runs after install in all code paths, but only when
        // the command opted in (e.g. `sdk install --update-global-json`).
        // The init flow intentionally does NOT own this — only command-level
        // flags control whether the global.json file is mutated.
        if (_command.UpdateGlobalJson)
        {
            _command.DotnetEnvironment.ApplyGlobalJsonModifications(requests);
        }
    }

    /// <summary>
    /// Generates resolved install requests for the given component specifications.
    /// Handles path resolution, global.json channel inference (for SDK when no channel specified),
    /// install path validation, and version resolution via the channel version resolver.
    /// </summary>
    public List<ResolvedInstallRequest> GenerateInstallRequests(
        MinimalInstallSpec[] componentSpecs)
    {
        var globalJson = GlobalJsonModifier.GetGlobalJsonInfo(Environment.CurrentDirectory);
        var currentInstallRoot = _command.DotnetEnvironment.GetCurrentPathConfiguration();

        var pathResolution = _installPathResolver.Resolve(
            _command.InstallPath,
            globalJson,
            currentInstallRoot);

        ValidateInstallPath(pathResolution.ResolvedInstallPath, pathResolution.PathSource, _command.ManifestPath);

        var installRoot = new DotnetInstallRoot(
            pathResolution.ResolvedInstallPath,
            InstallerUtilities.GetDefaultInstallArchitecture());

        var requests = new List<ResolvedInstallRequest>();
        foreach (var spec in componentSpecs)
        {
            var resolved = ResolveSpec(spec, installRoot, globalJson, currentInstallRoot, pathResolution);
            requests.Add(resolved);
        }

        return requests;
    }

    /// <summary>
    /// Resolves a single <see cref="MinimalInstallSpec"/> to a <see cref="ResolvedInstallRequest"/>
    /// by inferring the channel (from global.json or "latest"), resolving the version, and recording telemetry.
    /// </summary>
    private ResolvedInstallRequest ResolveSpec(
        MinimalInstallSpec spec,
        DotnetInstallRoot installRoot,
        GlobalJsonInfo? globalJson,
        DotnetInstallRootConfiguration? currentInstallRoot,
        InstallPathResolver.InstallPathResolutionResult pathResolution)
    {
        var component = spec.Component;
        var explicitChannel = spec.VersionOrChannel;

        var (channel, isFromGlobalJson) = ResolveChannel(component, explicitChannel, globalJson);

        var request = new DotnetInstallRequest(
            installRoot,
            new UpdateChannel(channel),
            component,
            new InstallRequestOptions
            {
                ManifestPath = _command.ManifestPath,
                RequireMuxerUpdate = _command.RequireMuxerUpdate,
                InstallSource = isFromGlobalJson ? InstallRequestSource.GlobalJson : InstallRequestSource.Explicit,
                GlobalJsonPath = (isFromGlobalJson || _command.UpdateGlobalJson) ? globalJson?.GlobalJsonPath : null,
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
            component, explicitChannel,
            _command.InstallPath, globalJson, currentInstallRoot,
            pathResolution, channel, resolved);

        return resolved;
    }

    /// <summary>
    /// Determines the channel for an install spec. If the user provided an explicit channel, uses that.
    /// For SDK installs with no explicit channel, tries to infer from global.json.
    /// Falls back to "latest" if nothing else applies.
    /// </summary>
    private static (string Channel, bool IsFromGlobalJson) ResolveChannel(
        InstallComponent component,
        string? explicitChannel,
        GlobalJsonInfo? globalJson)
    {
        if (explicitChannel is not null)
        {
            return (explicitChannel, false);
        }

        if (component == InstallComponent.SDK && globalJson?.GlobalJsonPath is not null)
        {
            string? channelFromGlobalJson = GlobalJsonChannelResolver.ResolveChannel(globalJson.GlobalJsonPath);
            if (channelFromGlobalJson is not null)
            {
                SpectreAnsiConsole.MarkupLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "[{0}]{1} {2} will be installed since {3} specifies that version.[/]",
                    DotnetupTheme.Current.Dim,
                    component.GetDisplayName(),
                    channelFromGlobalJson,
                    globalJson.GlobalJsonPath));
                return (channelFromGlobalJson, true);
            }
        }

        return (ChannelVersionResolver.LatestChannel, false);
    }

    /// <summary>
    /// Executes resolved install requests via the install executor as a concurrent batch.
    /// </summary>
    private void ExecuteInstallRequests(List<ResolvedInstallRequest> requests)
    {
        var batchResult = InstallExecutor.ExecuteInstalls(requests, _command.NoProgress);

        int newlyInstalled = batchResult.Successes.Count(r => !r.WasAlreadyInstalled);
        int alreadyInstalled = batchResult.Successes.Count(r => r.WasAlreadyInstalled);
        _command.SetCommandTag(TelemetryTagNames.InstallResult, $"installed:{newlyInstalled},already_installed:{alreadyInstalled}");

        if (batchResult.Failures.Count > 0)
        {
            ExceptionDispatchInfo.Capture(batchResult.Failures[0].Exception).Throw();
        }
    }

    private void ValidateInstallPath(string installPath, PathSource pathSource, string? manifestPath)
    {
        // Block install paths that point to existing files (not directories)
        if (File.Exists(installPath))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.InstallPathIsFile,
                $"The install path '{installPath}' is an existing file, not a directory. " +
                "Please specify a directory path for the installation.");
        }

        // Block system-managed install paths — dotnetup should not install there
        if (InstallPathClassifier.IsAdminInstallPath(installPath))
        {
            _command.SetCommandTag(TelemetryTagNames.InstallPathType, "system");
            _command.SetCommandTag(TelemetryTagNames.InstallPathSource, pathSource.ToString().ToLowerInvariant());
            throw new DotnetInstallException(
                DotnetInstallErrorCode.AdminPathBlocked,
                $"The install path '{installPath}' is a system-managed .NET location. " +
                "dotnetup cannot install to the default system .NET directory (Program Files\\dotnet on Windows, /usr/share/dotnet on Linux/macOS). " +
                "Use your system package manager or the official installer for system-wide installations, or choose a different path.");
        }

        if (!_command.Untracked)
        {
            ValidateNoUntrackedArtifacts(installPath, manifestPath);
        }
    }

    /// <summary>
    /// Throws if the install path contains .NET artifacts that are not tracked in the manifest.
    /// This is intentionally skipped when <c>--untracked</c> is specified, since untracked installs
    /// are expected to coexist with artifacts not in the manifest.
    /// </summary>
    internal static void ValidateNoUntrackedArtifacts(string installPath, string? manifestPath)
    {
        var installRoot = new DotnetInstallRoot(installPath, InstallerUtilities.GetDefaultInstallArchitecture());
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(manifestPath);
        if (!manifest.IsRootTracked(installRoot)
            && DotnetupSharedManifest.HasDotnetArtifacts(installRoot.Path))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.Unknown,
                $"The install path '{installRoot.Path}' already contains a .NET installation that is not tracked by dotnetup. " +
                "To avoid conflicts, use a different install path, remove the existing installation first, " +
                "or use the --untracked option to install without tracking.");
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
        string requestSource = requestedVersionOrChannel is not null
            ? "explicit"
            : globalJson?.GlobalJsonPath is not null
                ? "default-globaljson"
                : "default-latest";

        _command.SetCommandTag(TelemetryTagNames.InstallComponent, component.ToString());
        _command.SetCommandTag(TelemetryTagNames.InstallRequestedVersion, VersionSanitizer.Sanitize(requestedVersionOrChannel));
        _command.SetCommandTag(TelemetryTagNames.InstallPathExplicit, (explicitInstallPath is not null).ToString());
        _command.SetCommandTag(TelemetryTagNames.InstallHasGlobalJson, (globalJson?.GlobalJsonPath is not null).ToString());
        _command.SetCommandTag(TelemetryTagNames.InstallExistingInstallType, currentInstallRoot?.InstallType.ToString() ?? "none");
        _command.SetCommandTag(TelemetryTagNames.InstallPathType, InstallPathClassifier.ClassifyInstallPath(pathResolution.ResolvedInstallPath, pathResolution.PathSource));
        _command.SetCommandTag(TelemetryTagNames.InstallPathSource, pathResolution.PathSource.ToString().ToLowerInvariant());
        _command.SetCommandTag(TelemetryTagNames.InstallResolvedVersion, resolved.ResolvedVersion.ToString());
        _command.SetCommandTag(TelemetryTagNames.DotnetRequestSource, requestSource);
        _command.SetCommandTag(TelemetryTagNames.DotnetRequested, VersionSanitizer.Sanitize(resolvedChannel));
    }
}
