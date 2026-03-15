// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
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
        bool? SetDefaultInstall,
        string? ManifestPath,
        bool Interactive,
        bool NoProgress,
        InstallComponent Component,
        string ComponentDescription,
        bool? UpdateGlobalJson = null,
        Func<string, string?>? ResolveChannelFromGlobalJson = null,
        bool RequireMuxerUpdate = false,
        bool Untracked = false);

    /// <summary>
    /// Holds all resolved state during workflow execution, eliminating repeated parameter passing.
    /// </summary>
    private record WorkflowContext(
        InstallWorkflowOptions Options,
        InstallWalkthrough Walkthrough,
        GlobalJsonInfo? GlobalJson,
        DotnetInstallRootConfiguration? CurrentInstallRoot,
        string InstallPath,
        string? InstallPathFromGlobalJson,
        string Channel,
        bool SetDefaultInstall,
        bool? UpdateGlobalJson,
        string RequestSource,
        PathSource PathSource);

    public void Execute(InstallWorkflowOptions options)
    {
        // Record telemetry for the install request
        Activity.Current?.SetTag(TelemetryTagNames.InstallComponent, options.Component.ToString());
        Activity.Current?.SetTag(TelemetryTagNames.InstallRequestedVersion, VersionSanitizer.Sanitize(options.VersionOrChannel));
        Activity.Current?.SetTag(TelemetryTagNames.InstallPathExplicit, options.InstallPath is not null);

        var context = ResolveWorkflowContext(options, out string? error);
        if (context is null)
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.ContextResolutionFailed, error ?? "Failed to resolve workflow context.");
        }

        // Block install paths that point to existing files (not directories)
        if (File.Exists(context.InstallPath))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.InstallPathIsFile,
                $"The install path '{context.InstallPath}' is an existing file, not a directory. " +
                "Please specify a directory path for the installation.");
        }

        // Block admin/system-managed install paths — dotnetup should not install there
        if (InstallExecutor.IsAdminInstallPath(context.InstallPath))
        {
            Activity.Current?.SetTag(TelemetryTagNames.InstallPathType, "admin");
            Activity.Current?.SetTag(TelemetryTagNames.InstallPathSource, context.PathSource.ToString().ToLowerInvariant());
            throw new DotnetInstallException(
                DotnetInstallErrorCode.AdminPathBlocked,
                $"The install path '{context.InstallPath}' is a system-managed .NET location. " +
                "dotnetup cannot install to the default system .NET directory (Program Files\\dotnet on Windows, /usr/share/dotnet on Linux/macOS). " +
                "Use your system package manager or the official installer for system-wide installations, or choose a different path.");
        }

        // Record resolved context telemetry
        Activity.Current?.SetTag(TelemetryTagNames.InstallHasGlobalJson, context.GlobalJson?.GlobalJsonPath is not null);
        Activity.Current?.SetTag(TelemetryTagNames.InstallExistingInstallType, context.CurrentInstallRoot?.InstallType.ToString() ?? "none");
        Activity.Current?.SetTag(TelemetryTagNames.InstallSetDefault, context.SetDefaultInstall);
        Activity.Current?.SetTag(TelemetryTagNames.InstallPathType, InstallExecutor.ClassifyInstallPath(context.InstallPath, context.PathSource));
        Activity.Current?.SetTag(TelemetryTagNames.InstallPathSource, context.PathSource.ToString().ToLowerInvariant());

        // Record request source (how the version/channel was determined)
        Activity.Current?.SetTag(TelemetryTagNames.DotnetRequestSource, context.RequestSource);
        Activity.Current?.SetTag(TelemetryTagNames.DotnetRequested, VersionSanitizer.Sanitize(context.Channel));

        var resolved = CreateInstallRequest(context);

        // Record resolved version
        Activity.Current?.SetTag(TelemetryTagNames.InstallResolvedVersion, resolved.ResolvedVersion?.ToString());

        var installResult = ExecuteInstallations(context, resolved);

        ApplyPostInstallConfiguration(context, resolved);

        Activity.Current?.SetTag(TelemetryTagNames.InstallResult, installResult.WasAlreadyInstalled ? "already_installed" : "installed");
        InstallExecutor.DisplayComplete();
    }

    private WorkflowContext? ResolveWorkflowContext(InstallWorkflowOptions options, out string? error)
    {
        var walkthrough = new InstallWalkthrough(_dotnetInstaller, options);
        var globalJson = _dotnetInstaller.GetGlobalJsonInfo(Environment.CurrentDirectory);
        var currentInstallRoot = _dotnetInstaller.GetConfiguredInstallType();

        var pathResolution = _installPathResolver.Resolve(
            options.InstallPath,
            globalJson,
            currentInstallRoot,
            options.Interactive,
            options.ComponentDescription,
            out error);

        if (pathResolution is null)
        {
            return null;
        }

        string? channelFromGlobalJson = null;
        bool? updateGlobalJson = null;

        if (options.ResolveChannelFromGlobalJson is not null && globalJson?.GlobalJsonPath is not null)
        {
            channelFromGlobalJson = options.ResolveChannelFromGlobalJson(globalJson.GlobalJsonPath);
            updateGlobalJson = walkthrough.ResolveUpdateGlobalJson(channelFromGlobalJson);
        }

        string channel = walkthrough.ResolveChannel(channelFromGlobalJson, globalJson?.GlobalJsonPath);
        bool setDefaultInstall = walkthrough.ResolveSetDefaultInstall(
            currentInstallRoot,
            pathResolution.ResolvedInstallPath,
            installPathCameFromGlobalJson: pathResolution.InstallPathFromGlobalJson is not null);

        // Classify how the version/channel was determined for telemetry
        string requestSource = options.VersionOrChannel is not null
            ? "explicit"
            : channelFromGlobalJson is not null
                ? "default-globaljson"
                : "default-latest";

        return new WorkflowContext(
            options,
            walkthrough,
            globalJson,
            currentInstallRoot,
            pathResolution.ResolvedInstallPath,
            pathResolution.InstallPathFromGlobalJson,
            channel,
            setDefaultInstall,
            updateGlobalJson,
            requestSource,
            pathResolution.PathSource);
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
            context.Options.Untracked);
    }

    private static InstallExecutor.InstallResult ExecuteInstallations(WorkflowContext context, InstallExecutor.ResolvedInstallRequest resolved)
    {
        // Gather all user prompts before starting any downloads.
        // Users may walk away after seeing download progress begin, expecting no more prompts.
        var additionalVersions = context.Walkthrough.GetAdditionalAdminVersionsToMigrate(
            resolved.ResolvedVersion,
            context.SetDefaultInstall,
            context.CurrentInstallRoot);

        var installResult = InstallExecutor.ExecuteInstall(
            resolved.Request,
            resolved.ResolvedVersion?.ToString(),
            context.Options.ComponentDescription,
            context.Options.NoProgress);

        InstallExecutor.ExecuteAdditionalInstalls(
            additionalVersions,
            resolved.Request.InstallRoot,
            context.Options.Component,
            context.Options.ComponentDescription,
            context.Options.ManifestPath,
            context.Options.NoProgress,
            context.Options.RequireMuxerUpdate);

        return installResult;
    }

    private void ApplyPostInstallConfiguration(WorkflowContext context, InstallExecutor.ResolvedInstallRequest resolved)
    {
        InstallExecutor.ConfigureDefaultInstallIfRequested(_dotnetInstaller, context.SetDefaultInstall, context.InstallPath);

        if (context.UpdateGlobalJson == true && context.GlobalJson?.GlobalJsonPath is not null)
        {
            _dotnetInstaller.UpdateGlobalJson(
                context.GlobalJson.GlobalJsonPath,
                resolved.ResolvedVersion!.ToString());
        }
    }

}
