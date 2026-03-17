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
        bool? SetDefaultInstall,
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
        DotnetInstallRoot? InstallRoot,
        string? ManifestPath,
        bool NoProgress,
        bool RequireMuxerUpdate);

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

    public InstallWorkflowResult Execute(InstallWorkflowOptions options)
    {
        var context = ResolveWorkflowContext(options, out string? error);
        if (context is null)
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.ContextResolutionFailed, error ?? "Failed to resolve workflow context.");
        }

        ValidateInstallPath(context);

        var resolved = CreateInstallRequest(context);
        RecordTelemetry(options, context, resolved);

        var installResult = ExecuteInstallations(context, resolved);

        ApplyPostInstallConfiguration(context, resolved);

        Activity.Current?.SetTag(TelemetryTagNames.InstallResult, installResult.WasAlreadyInstalled ? "already_installed" : "installed");

        return new InstallWorkflowResult(
            resolved.ResolvedVersion,
            resolved.Request.InstallRoot,
            options.ManifestPath,
            options.NoProgress,
            options.RequireMuxerUpdate);
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
    }

    private static void RecordTelemetry(InstallWorkflowOptions options, WorkflowContext context, InstallExecutor.ResolvedInstallRequest resolved)
    {
        // Request-level tags
        Activity.Current?.SetTag(TelemetryTagNames.InstallComponent, options.Component.ToString());
        Activity.Current?.SetTag(TelemetryTagNames.InstallRequestedVersion, VersionSanitizer.Sanitize(options.VersionOrChannel));
        Activity.Current?.SetTag(TelemetryTagNames.InstallPathExplicit, options.InstallPath is not null);

        // Resolved context tags
        Activity.Current?.SetTag(TelemetryTagNames.InstallHasGlobalJson, context.GlobalJson?.GlobalJsonPath is not null);
        Activity.Current?.SetTag(TelemetryTagNames.InstallExistingInstallType, context.CurrentInstallRoot?.InstallType.ToString() ?? "none");
        Activity.Current?.SetTag(TelemetryTagNames.InstallSetDefault, context.SetDefaultInstall);
        Activity.Current?.SetTag(TelemetryTagNames.InstallPathType, InstallExecutor.ClassifyInstallPath(context.InstallPath, context.PathSource));
        Activity.Current?.SetTag(TelemetryTagNames.InstallPathSource, context.PathSource.ToString().ToLowerInvariant());

        // Resolved version tags
        Activity.Current?.SetTag(TelemetryTagNames.InstallResolvedVersion, resolved.ResolvedVersion?.ToString());
        Activity.Current?.SetTag(TelemetryTagNames.DotnetRequestSource, context.RequestSource);
        Activity.Current?.SetTag(TelemetryTagNames.DotnetRequested, VersionSanitizer.Sanitize(context.Channel));
    }

    private WorkflowContext? ResolveWorkflowContext(InstallWorkflowOptions options, out string? error)
    {
        var walkthrough = new InstallWalkthrough(_dotnetInstaller, _channelVersionResolver, options);
        var globalJson = _dotnetInstaller.GetGlobalJsonInfo(Environment.CurrentDirectory);
        var currentInstallRoot = _dotnetInstaller.GetConfiguredInstallType();

        var pathResolution = _installPathResolver.Resolve(
            options.InstallPath,
            globalJson,
            currentInstallRoot,
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
        bool setDefaultInstall = DeriveSetDefaultInstall(options);

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

    /// <summary>
    /// Determines whether the installation should be set as the system default.
    /// Checks the saved config first; only shows prompts when the config is absent.
    /// </summary>
    private static bool DeriveSetDefaultInstall(
        InstallWorkflowOptions options)
    {
        // If the caller already determined this (e.g. WalkthroughCommand), use it directly.
        if (options.SetDefaultInstall is not null)
        {
            return options.SetDefaultInstall.Value;
        }

        // If a PathPreference was passed in options or is already saved in config, derive silently — no prompts.
        var savedPreference = options.PathPreference ?? DotnetupConfig.Read()?.PathPreference;
        if (savedPreference is not null)
        {
            return savedPreference != PathPreference.DotnetupDotnet;
        }

        // No config yet. If interactive, show the full path preference selector
        // (delegates to WalkthroughCommand.PromptPathPreference), saves to config, and returns the choice.
        if (options.Interactive)
        {
            var preference = DotnetupConfig.EnsurePathPreference(interactive: true);
            if (preference is not null)
            {
                return preference != PathPreference.DotnetupDotnet;
            }
        }

        // Non-interactive with no config: default to not setting the default install.
        return false;
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

    private static InstallExecutor.InstallResult ExecuteInstallations(WorkflowContext context, InstallExecutor.ResolvedInstallRequest resolved)
    {
        // Always install the primary component first so the user can start working quickly.
        var primaryResult = InstallExecutor.ExecuteInstall(
            resolved.Request,
            resolved.ResolvedVersion?.ToString(),
            context.Options.ComponentDescription,
            context.Options.NoProgress);

        // Copy admin installs after the primary install completes.
        var additionalInstalls = context.Walkthrough.GetAdditionalAdminVersionsToMigrate(
            resolved.ResolvedVersion,
            context.SetDefaultInstall);

        if (additionalInstalls.Count > 0)
        {
            InstallExecutor.ExecuteAdditionalInstalls(
                additionalInstalls,
                resolved.Request.InstallRoot,
                context.Options.ManifestPath,
                context.Options.NoProgress,
                context.Options.RequireMuxerUpdate);
        }

        return primaryResult;
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
