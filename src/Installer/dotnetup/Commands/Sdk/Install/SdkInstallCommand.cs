// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;

internal class SdkInstallCommand(ParseResult result) : CommandBase(result)
{
    private readonly string[] _channels = result.GetValue(SdkInstallCommandParser.ChannelArguments) ?? [];
    private readonly string? _installPath = result.GetValue(CommonOptions.InstallPathOption);
    private readonly bool? _setDefaultInstall = result.GetValue(CommonOptions.SetDefaultInstallOption);
    private readonly bool? _updateGlobalJson = result.GetValue(SdkInstallCommandParser.UpdateGlobalJsonOption);
    private readonly string? _manifestPath = result.GetValue(CommonOptions.ManifestPathOption);
    private readonly bool _interactive = result.GetValue(CommonOptions.InteractiveOption);
    private readonly bool _noProgress = result.GetValue(CommonOptions.NoProgressOption);
    private readonly Verbosity _verbosity = result.GetValue(CommonOptions.VerbosityOption);
    private readonly bool _requireMuxerUpdate = result.GetValue(CommonOptions.RequireMuxerUpdateOption);
    private readonly bool _untracked = result.GetValue(CommonOptions.UntrackedOption);

    private readonly DotnetInstallManager _dotnetInstaller = new();
    private readonly ChannelVersionResolver _channelVersionResolver = new();

    protected override string GetCommandName() => "sdk/install";

    protected override int ExecuteCore()
    {
        // Single channel (or none): use existing InstallWorkflow for full walkthrough support
        if (_channels.Length <= 1)
        {
            string? singleChannel = _channels.Length == 1 ? _channels[0] : null;
            return ExecuteSingleInstall(singleChannel);
        }

        // Multiple channels: validate all upfront, then download concurrently and commit sequentially
        return ExecuteMultipleInstalls(_channels);
    }

    private int ExecuteSingleInstall(string? versionOrChannel)
    {

        var workflow = new InstallWorkflow(_dotnetInstaller, _channelVersionResolver);

        var options = new InstallWorkflow.InstallWorkflowOptions(
            versionOrChannel,
            _installPath,
            setDefault,
            _manifestPath,
            _interactive,
            _noProgress,
            InstallComponent.SDK,
            InstallComponent.SDK.GetDisplayName(),
            _updateGlobalJson,
            GlobalJsonChannelResolver.ResolveChannel,
            _requireMuxerUpdate,
            _untracked,
            pathPreference,
            Verbosity: _verbosity);

        workflow.Execute(options);
        return 0;
    }

    private int ExecuteMultipleInstalls(string[] channels)
    {
        var (_, setDefault) = InstallExecutor.ResolveInstallDefaults(_interactive, _setDefaultInstall, _installPath);
        string installPath = _installPath ?? _dotnetInstaller.GetDefaultDotnetInstallPath();
        var installRoot = new DotnetInstallRoot(installPath, InstallerUtilities.GetDefaultInstallArchitecture());

        var requests = channels.Select(channel => new DotnetInstallRequest(
            installRoot,
            new UpdateChannel(channel),
            InstallComponent.SDK,
            new InstallRequestOptions
            {
                ManifestPath = _manifestPath,
                RequireMuxerUpdate = _requireMuxerUpdate,
                Untracked = _untracked,
                Verbosity = _verbosity
            })).ToList();

        InstallExecutor.RunMultiInstall(requests, installPath, _noProgress, setDefault, _dotnetInstaller);

        return 0;
    }
}
