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
    private readonly InstallWalkthrough _installWalkthrough;

    public InstallWorkflow(IDotnetInstallManager dotnetInstaller, ChannelVersionResolver channelVersionResolver)
    {
        _dotnetInstaller = dotnetInstaller;
        _channelVersionResolver = channelVersionResolver;
        _installPathResolver = new InstallPathResolver(dotnetInstaller);
        _installWalkthrough = new InstallWalkthrough(dotnetInstaller, channelVersionResolver);
    }

    /// <summary>
    /// Options for the install workflow.
    /// </summary>
    /// <remarks>
    /// TODO: Consider refactoring to use a builder pattern to reduce the number of constructor parameters.
    /// This would make the API more flexible and easier to extend with new options.
    /// </remarks>
    public record InstallWorkflowOptions(
        string? VersionOrChannel,
        string? InstallPath,
        bool? SetDefaultInstall,
        string? ManifestPath,
        bool Interactive,
        bool NoProgress,
        InstallComponent Component,
        string ComponentDescription,
        // SDK-specific options (optional for runtime)
        bool? UpdateGlobalJson = null,
        Func<string, string?>? ResolveChannelFromGlobalJson = null);

    /// <summary>
    /// Result of the install workflow.
    /// </summary>
    public record InstallWorkflowResult(int ExitCode, InstallExecutor.ResolvedInstallRequest? ResolvedRequest);

    /// <summary>
    /// Executes the common installation workflow.
    /// </summary>
    public InstallWorkflowResult Execute(InstallWorkflowOptions options)
    {
        GlobalJsonInfo? globalJsonInfo = _dotnetInstaller.GetGlobalJsonInfo(Environment.CurrentDirectory);
        DotnetInstallRootConfiguration? currentDotnetInstallRoot = _dotnetInstaller.GetConfiguredInstallType();

        // Resolve the install path
        InstallPathResolver.InstallPathResolutionResult? pathResolution = _installPathResolver.Resolve(
            options.InstallPath,
            globalJsonInfo,
            currentDotnetInstallRoot,
            options.Interactive,
            options.ComponentDescription,
            out string? error);

        if (pathResolution == null)
        {
            Console.Error.WriteLine(error);
            return new InstallWorkflowResult(1, null);
        }

        string resolvedInstallPath = pathResolution.ResolvedInstallPath;
        string? installPathFromGlobalJson = pathResolution.InstallPathFromGlobalJson;

        // Handle global.json channel resolution (SDK-specific)
        string? channelFromGlobalJson = null;
        bool? resolvedUpdateGlobalJson = null;

        if (options.ResolveChannelFromGlobalJson is not null && globalJsonInfo?.GlobalJsonPath is not null)
        {
            channelFromGlobalJson = options.ResolveChannelFromGlobalJson(globalJsonInfo.GlobalJsonPath);

            resolvedUpdateGlobalJson = _installWalkthrough.ResolveUpdateGlobalJson(
                channelFromGlobalJson,
                options.VersionOrChannel,
                options.UpdateGlobalJson,
                options.Interactive);
        }

        // Resolve the channel/version to install
        string resolvedChannel = _installWalkthrough.ResolveChannel(
            options.VersionOrChannel,
            channelFromGlobalJson,
            globalJsonInfo?.GlobalJsonPath,
            options.Interactive,
            options.ComponentDescription,
            options.Component);

        // Resolve whether to set this as the default install
        bool resolvedSetDefaultInstall = _installWalkthrough.ResolveSetDefaultInstall(
            options.SetDefaultInstall,
            currentDotnetInstallRoot,
            resolvedInstallPath,
            installPathFromGlobalJson,
            options.Interactive);

        // Create a request and resolve the version
        InstallExecutor.ResolvedInstallRequest resolved = InstallExecutor.CreateAndResolveRequest(
            resolvedInstallPath,
            resolvedChannel,
            options.Component,
            options.ManifestPath,
            _channelVersionResolver);

        // Check if user wants to migrate admin versions when switching to user install
        List<string> additionalVersionsToInstall = _installWalkthrough.GetAdditionalAdminVersionsToMigrate(
            resolved.ResolvedVersion,
            resolvedSetDefaultInstall,
            currentDotnetInstallRoot,
            options.Interactive,
            options.ComponentDescription);

        // Execute the installation
        InstallExecutor.InstallResult installResult = InstallExecutor.ExecuteInstall(
            resolved.Request,
            resolved.ResolvedVersion?.ToString(),
            options.ComponentDescription,
            options.NoProgress);

        if (!installResult.Success)
        {
            return new InstallWorkflowResult(1, resolved);
        }

        // Install any additional versions
        InstallExecutor.ExecuteAdditionalInstalls(
            additionalVersionsToInstall,
            resolved.Request.InstallRoot,
            options.Component,
            options.ComponentDescription,
            options.ManifestPath,
            options.NoProgress);

        // Configure default install if requested
        InstallExecutor.ConfigureDefaultInstallIfRequested(_dotnetInstaller, resolvedSetDefaultInstall, resolvedInstallPath);

        // Handle global.json update (SDK-specific)
        if (resolvedUpdateGlobalJson == true && globalJsonInfo?.GlobalJsonPath is not null)
        {
            _dotnetInstaller.UpdateGlobalJson(
                globalJsonInfo.GlobalJsonPath,
                resolved.ResolvedVersion!.ToString(),
                globalJsonInfo.AllowPrerelease,
                globalJsonInfo.RollForward);
        }

        InstallExecutor.DisplayComplete();

        return new InstallWorkflowResult(0, resolved);
    }
}
