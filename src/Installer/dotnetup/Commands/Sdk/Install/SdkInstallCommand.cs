// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;

internal class SdkInstallCommand(ParseResult result) : CommandBase(result)
{
    private readonly string? _versionOrChannel = result.GetValue(SdkInstallCommandParser.ChannelArgument);
    private readonly string? _installPath = result.GetValue(CommonOptions.InstallPathOption);
    private readonly bool? _setDefaultInstall = result.GetValue(CommonOptions.SetDefaultInstallOption);
    private readonly bool? _updateGlobalJson = result.GetValue(SdkInstallCommandParser.UpdateGlobalJsonOption);
    private readonly string? _manifestPath = result.GetValue(CommonOptions.ManifestPathOption);
    private readonly bool _interactive = result.GetValue(CommonOptions.InteractiveOption);
    private readonly bool _noProgress = result.GetValue(CommonOptions.NoProgressOption);
    private readonly bool _requireMuxerUpdate = result.GetValue(CommonOptions.RequireMuxerUpdateOption);
    private readonly bool _untracked = result.GetValue(CommonOptions.UntrackedOption);

    private readonly IDotnetInstallManager _dotnetInstaller = new DotnetInstallManager();
    private readonly ChannelVersionResolver _channelVersionResolver = new();

    protected override string GetCommandName() => "sdk/install";

    protected override int ExecuteCore()
    {
        var workflow = new InstallWorkflow(_dotnetInstaller, _channelVersionResolver);

        var options = new InstallWorkflow.InstallWorkflowOptions(
            _versionOrChannel,
            _installPath,
            _setDefaultInstall,
            _manifestPath,
            _interactive,
            _noProgress,
            InstallComponent.SDK,
            ".NET SDK",
            _updateGlobalJson,
            GlobalJsonChannelResolver.ResolveChannel,
            _requireMuxerUpdate,
            _untracked);

        workflow.Execute(options);
        return 0;
    }
}
