// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
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
        bool RequireMuxerUpdate = false);

    public record InstallWorkflowResult(int ExitCode, InstallExecutor.ResolvedInstallRequest? ResolvedRequest);

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
        // Record telemetry for the install request
        Activity.Current?.SetTag(TelemetryTagNames.InstallComponent, options.Component.ToString());
        Activity.Current?.SetTag(TelemetryTagNames.InstallRequestedVersion, VersionSanitizer.Sanitize(options.VersionOrChannel));
        Activity.Current?.SetTag(TelemetryTagNames.InstallPathExplicit, options.InstallPath is not null);

        var context = ResolveWorkflowContext(options, out string? error);
        if (context is null)
        {
            Console.Error.WriteLine(error);
            Activity.Current?.SetTag(TelemetryTagNames.ErrorType, "context_resolution_failed");
            Activity.Current?.SetTag(TelemetryTagNames.ErrorCategory, "user");
            return new InstallWorkflowResult(1, null);
        }

        // Block admin/system-managed install paths — dotnetup should not install there
        if (InstallExecutor.IsAdminInstallPath(context.InstallPath))
        {
            Console.Error.WriteLine($"Error: The install path '{context.InstallPath}' is a system-managed .NET location. " +
                "dotnetup cannot install to the default system .NET directory (Program Files\\dotnet on Windows, /usr/share/dotnet on Linux/macOS). " +
                "Use your system package manager or the official installer for system-wide installations, or choose a different path.");
            Activity.Current?.SetTag(TelemetryTagNames.ErrorType, "admin_path_blocked");
            Activity.Current?.SetTag(TelemetryTagNames.InstallPathType, "admin");
            Activity.Current?.SetTag(TelemetryTagNames.InstallPathSource, context.PathSource.ToString().ToLowerInvariant());
            Activity.Current?.SetTag(TelemetryTagNames.ErrorCategory, "user");
            return new InstallWorkflowResult(1, null);
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
        if (installResult is null)
        {
            Activity.Current?.SetTag(TelemetryTagNames.ErrorType, "install_failed");
            Activity.Current?.SetTag(TelemetryTagNames.ErrorCategory, "product");
            return new InstallWorkflowResult(1, resolved);
        }

        ApplyPostInstallConfiguration(context, resolved);

        Activity.Current?.SetTag(TelemetryTagNames.InstallResult, installResult.WasAlreadyInstalled ? "already_installed" : "installed");
        InstallExecutor.DisplayComplete();
        return new InstallWorkflowResult(0, resolved);
    }

    private WorkflowContext? ResolveWorkflowContext(InstallWorkflowOptions options, out string? error)
    {
        error = null;
        var walkthrough = new InstallWalkthrough(_dotnetInstaller, _channelVersionResolver, options);
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
        string requestSource = channelFromGlobalJson is not null
            ? "default-globaljson"
            : options.VersionOrChannel is not null
                ? "explicit"
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
        return InstallExecutor.CreateAndResolveRequest(
            context.InstallPath,
            context.Channel,
            context.Options.Component,
            context.Options.ManifestPath,
            _channelVersionResolver,
            context.Options.RequireMuxerUpdate);
    }

    private InstallExecutor.InstallResult? ExecuteInstallations(WorkflowContext context, InstallExecutor.ResolvedInstallRequest resolved)
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

        if (!installResult.Success)
        {
            return null;
        }

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
                resolved.ResolvedVersion!.ToString(),
                context.GlobalJson.AllowPrerelease,
                context.GlobalJson.RollForward);
        }
    }
}
