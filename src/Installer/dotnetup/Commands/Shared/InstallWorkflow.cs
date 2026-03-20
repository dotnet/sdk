// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Shared installation workflow that handles the common installation logic for SDK and Runtime commands.
/// </summary>
internal class InstallWorkflow
{
    private readonly IDotnetInstallManager _dotnetInstaller;
    private readonly ChannelVersionResolver _channelVersionResolver;
    private readonly InstallPathResolver _installPathResolver;

    public InstallWorkflow(IDotnetInstallManager dotnetInstaller, ChannelVersionResolver channelVersionResolver)
    {
        _dotnetInstaller = dotnetInstaller;
        _channelVersionResolver = channelVersionResolver;
        _installPathResolver = new InstallPathResolver(dotnetInstaller);
    }

    public record InstallWorkflowOptions(
        string? VersionOrChannel,
        string? InstallPath,
        bool? ReplaceSystemConfig,
        string? ManifestPath,
        bool Interactive,
        bool NoProgress,
        InstallComponent Component,
        string ComponentDescription,
        bool? UpdateGlobalJson = null,
        Func<string, string?>? ResolveChannelFromGlobalJson = null,
        bool RequireMuxerUpdate = false,
        bool Untracked = false,
        PathPreference? PathPreference = null,
        Verbosity Verbosity = Verbosity.Normal);

    /// <summary>
    /// Result of the install workflow, including any deferred additional installs.
    /// </summary>
    public record InstallWorkflowResult(
        ReleaseVersion? ResolvedVersion,
        DotnetInstallRoot? InstallRoot);

    /// <summary>
    /// Holds all resolved state during workflow execution, eliminating repeated parameter passing.
    /// </summary>
    private record WorkflowContext(
        InstallWorkflowOptions Options,
        GlobalJsonInfo? GlobalJson,
        DotnetInstallRootConfiguration? CurrentInstallRoot,
        string InstallPath,
        string? InstallPathFromGlobalJson,
        string Channel,
        string RequestSource,
        PathSource PathSource);


    /**
     - Validates the install path - an invalid path may be ok to send to this function
    **/
    public InstallWorkflowResult Execute(InstallWorkflowOptions options)
    {
        // RF: this function should really be calling the minimal walkthrough and wrapping this existing function in another function given to the walkthrough
        // Call the minimal walkthrough and send it the install function to run
        // If the install path is not null, then don't call the walkthrough and  simply execute the install
        
    }

    public InstallWorkflowResult Install()
    {
        var resolved = GenerateInstallRequest(options);
        RecordTelemetry(options, context, resolved);

        var installResult = ExecuteInstallation(context, resolved);

        Activity.Current?.SetTag(TelemetryTagNames.InstallResult, installResult.WasAlreadyInstalled ? "already_installed" : "installed");

        return new InstallWorkflowResult(
            resolved.ResolvedVersion,
            resolved.Request.InstallRoot);
    }

        public static InstallRequest? GenerateInstallRequest(InstallWorkflowOptions options, out string? error)
    {
        var globalJson = GlobalJsonModifier.GetGlobalJsonInfo(Environment.CurrentDirectory);
        var currentInstallRoot = DotnetInstallManager.GetCurrentPathConfiguration();

        var pathToInstallTo = _installPathResolver.Resolve(
            options.InstallPath,
            globalJson,
            currentInstallRoot,
            out error);

        if (pathToInstallTo is null)
        {
            return null;
        }

        ValidateInstallPath(pathToInstallTo);

        string? channelFromGlobalJson = null;

        if (options.ResolveChannelFromGlobalJson is not null && globalJson?.GlobalJsonPath is not null)
        {
            channelFromGlobalJson = options.ResolveChannelFromGlobalJson(globalJson.GlobalJsonPath);
        }

        string channel = ResolveChannel(channelFromGlobalJson, globalJson?.GlobalJsonPath);

        // Classify how the version/channel was determined for telemetry
        string requestSource = options.VersionOrChannel is not null
            ? "explicit"
            : channelFromGlobalJson is not null
                ? "default-globaljson"
                : "default-latest";

        return CreateInstallRequest();
    }

     private static List<DotnetInstallRequest> GetMultipleInstallRequests(
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

    private static void ValidateInstallPath(WorkflowContext context)
    {
        // Block install paths that point to existing files (not directories)
        if (File.Exists(context.InstallPath))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.InstallPathIsFile,
                $"The install path '{context.InstallPath}' is an existing file, not a directory. " +
                "Please specify a directory path for the installation.");
        }

        // Block admin/system-managed install paths — dotnetup should not install there
        if (InstallPathClassifier.IsAdminInstallPath(context.InstallPath))
        {
            Activity.Current?.SetTag(TelemetryTagNames.InstallPathType, "admin");
            Activity.Current?.SetTag(TelemetryTagNames.InstallPathSource, context.PathSource.ToString().ToLowerInvariant());
            throw new DotnetInstallException(
                DotnetInstallErrorCode.AdminPathBlocked,
                $"The install path '{context.InstallPath}' is a system-managed .NET location. " +
                "dotnetup cannot install to the default system .NET directory (Program Files\\dotnet on Windows, /usr/share/dotnet on Linux/macOS). " +
                "Use your system package manager or the official installer for system-wide installations, or choose a different path.");
        }

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

    private static void RecordInstallTelemetry(InstallWorkflowOptions options, WorkflowContext context, InstallExecutor.ResolvedInstallRequest resolved)
    {
        // Request-level tags
        Activity.Current?.SetTag(TelemetryTagNames.InstallComponent, options.Component.ToString());
        Activity.Current?.SetTag(TelemetryTagNames.InstallRequestedVersion, VersionSanitizer.Sanitize(options.VersionOrChannel));
        Activity.Current?.SetTag(TelemetryTagNames.InstallPathExplicit, options.InstallPath is not null);

        // Resolved context tags
        Activity.Current?.SetTag(TelemetryTagNames.InstallHasGlobalJson, context.GlobalJson?.GlobalJsonPath is not null);
        Activity.Current?.SetTag(TelemetryTagNames.InstallExistingInstallType, context.CurrentInstallRoot?.InstallType.ToString() ?? "none");
        Activity.Current?.SetTag(TelemetryTagNames.InstallSetDefault, context.ReplaceSystemConfig);
        Activity.Current?.SetTag(TelemetryTagNames.InstallPathType, InstallExecutor.ClassifyInstallPath(context.InstallPath, context.PathSource));
        Activity.Current?.SetTag(TelemetryTagNames.InstallPathSource, context.PathSource.ToString().ToLowerInvariant());

        // Resolved version tags
        Activity.Current?.SetTag(TelemetryTagNames.InstallResolvedVersion, resolved.ResolvedVersion?.ToString());
        Activity.Current?.SetTag(TelemetryTagNames.DotnetRequestSource, context.RequestSource);
        Activity.Current?.SetTag(TelemetryTagNames.DotnetRequested, VersionSanitizer.Sanitize(context.Channel));
    }

    /// <summary>
    /// Resolves the channel or version to install, considering global.json and user input.
    /// </summary>
    /// <param name="channelFromGlobalJson">The channel resolved from global.json, if any.</param>
    /// <param name="globalJsonPath">Path to the global.json file, for display purposes.</param>
    /// <param name="defaultChannel">The default channel to use if none specified (typically "latest").</param>
    /// <returns>The resolved channel or version string.</returns>
    public string ResolveChannel(
        string? channelFromGlobalJson,
        string? globalJsonPath,
        string defaultChannel = "latest")
    {
        // Explicit version/channel from the user always takes priority
        if (_options.VersionOrChannel is not null)
        {
            return _options.VersionOrChannel;
        }

        if (channelFromGlobalJson is not null)
        {
            SpectreAnsiConsole.WriteLine($"{_options.ComponentDescription} {channelFromGlobalJson} will be installed since {globalJsonPath} specifies that version.");
            return channelFromGlobalJson;
        }

        return defaultChannel;
    }

    private InstallExecutor.ResolvedInstallRequest CreateInstallRequest(WorkflowContext context)
    {
        // Only tag as GlobalJson source if the channel/version actually came from global.json,
        // not just because a global.json file exists in the directory.
        var installSource = context.RequestSource == "default-globaljson"
            ? InstallRequestSource.GlobalJson
            : InstallRequestSource.Explicit;

        return InstallExecutor.CreateAndResolveRequest(
            context.InstallPath,
            context.Channel,
            context.Options.Component,
            context.Options.ManifestPath,
            _channelVersionResolver,
            context.Options.RequireMuxerUpdate,
            installSource,
            installSource == InstallRequestSource.GlobalJson ? context.GlobalJson?.GlobalJsonPath : null,
            context.Options.Untracked,
            context.Options.Verbosity);
    }



}
