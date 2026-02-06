// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

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
        Func<string, string?>? ResolveChannelFromGlobalJson = null);

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
        bool? UpdateGlobalJson);

    public InstallWorkflowResult Execute(InstallWorkflowOptions options)
    {
        var context = ResolveWorkflowContext(options, out string? error);
        if (context is null)
        {
            Console.Error.WriteLine(error);
            return new InstallWorkflowResult(1, null);
        }

        var resolved = CreateInstallRequest(context);

        if (!ExecuteInstallations(context, resolved))
        {
            return new InstallWorkflowResult(1, resolved);
        }

        ApplyPostInstallConfiguration(context, resolved);

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

        return new WorkflowContext(
            options,
            walkthrough,
            globalJson,
            currentInstallRoot,
            pathResolution.ResolvedInstallPath,
            pathResolution.InstallPathFromGlobalJson,
            channel,
            setDefaultInstall,
            updateGlobalJson);
    }

    private InstallExecutor.ResolvedInstallRequest CreateInstallRequest(WorkflowContext context)
    {
        return InstallExecutor.CreateAndResolveRequest(
            context.InstallPath,
            context.Channel,
            context.Options.Component,
            context.Options.ManifestPath,
            _channelVersionResolver);
    }

    private bool ExecuteInstallations(WorkflowContext context, InstallExecutor.ResolvedInstallRequest resolved)
    {
        var installResult = InstallExecutor.ExecuteInstall(
            resolved.Request,
            resolved.ResolvedVersion?.ToString(),
            context.Options.ComponentDescription,
            context.Options.NoProgress);

        if (!installResult.Success)
        {
            return false;
        }

        // TODO: Consider moving this prompt earlier, before any downloads start.
        // Users may walk away after seeing download progress begin, expecting no more prompts.
        // See: https://github.com/dotnet/sdk/pull/52649#discussion_r2760412186
        var additionalVersions = context.Walkthrough.GetAdditionalAdminVersionsToMigrate(
            resolved.ResolvedVersion,
            context.SetDefaultInstall,
            context.CurrentInstallRoot);

        InstallExecutor.ExecuteAdditionalInstalls(
            additionalVersions,
            resolved.Request.InstallRoot,
            context.Options.Component,
            context.Options.ComponentDescription,
            context.Options.ManifestPath,
            context.Options.NoProgress);

        return true;
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
