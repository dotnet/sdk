// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Globalization;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Walkthrough;

/// <summary>
/// Runs the interactive walkthrough that installs the .NET SDK with defaults
/// and records the user's path replacement preference to <c>dotnetup.config.json</c>.
/// </summary>
internal class WalkthroughCommand(ParseResult result) : CommandBase(result)
{
    private readonly string? _installPath = result.GetValue(CommonOptions.InstallPathOption);
    private readonly string? _manifestPath = result.GetValue(CommonOptions.ManifestPathOption);
    private readonly bool _noProgress = result.GetValue(CommonOptions.NoProgressOption);
    private readonly Verbosity _verbosity = result.GetValue(CommonOptions.VerbosityOption);
    private readonly bool _requireMuxerUpdate = result.GetValue(CommonOptions.RequireMuxerUpdateOption);

    private readonly DotnetInstallManager _dotnetInstaller = new();
    private readonly ChannelVersionResolver _channelVersionResolver = new();

    protected override string GetCommandName() => "walkthrough";

    protected override int ExecuteCore()
    {
        // Invoke the full walkthrough workflow passing in the given command options
        return 0;
    }

    /// <summary>
    /// Resolves the install channel from global.json (or the user's selection) and fires off a
    /// background predownload to warm the cache while the user answers prompts.
    /// </summary>
    private (string Channel, GlobalJsonInfo? GlobalJson, DotnetInstallRoot InstallRoot) ResolveChannelAndStartPredownload(string selectedChannel)
    {


        _ = InstallerOrchestratorSingleton.PredownloadToCacheAsync(
            channel, InstallComponent.SDK, installRoot);

        return (channel, globalJson, installRoot);
    }

    private InstallWorkflow.InstallWorkflowResult RunInstallWorkflow(string channel, PathPreference pathPreference, bool? replaceSystemConfig)
    {
        var workflow = new InstallWorkflow(_dotnetInstaller, _channelVersionResolver);
        var options = new InstallWorkflow.InstallWorkflowOptions(
            VersionOrChannel: channel,
            InstallPath: _installPath,
            ReplaceSystemConfig: replaceSystemConfig,
            ManifestPath: _manifestPath,
            Interactive: true,
            NoProgress: _noProgress,
            Component: InstallComponent.SDK,
            ComponentDescription: InstallComponent.SDK.GetDisplayName(),
            UpdateGlobalJson: null,
            ResolveChannelFromGlobalJson: GlobalJsonChannelResolver.ResolveChannel,
            RequireMuxerUpdate: _requireMuxerUpdate,
            PathPreference: pathPreference,
            Verbosity: _verbosity);
        return workflow.Execute(options);
    }


}
