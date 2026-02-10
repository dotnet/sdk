// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;

internal class SdkInstallCommand(ParseResult result) : CommandBase(result)
{
    private readonly string? _versionOrChannel = result.GetValue(SdkInstallCommandParser.ChannelArgument);
    private readonly string? _installPath = result.GetValue(SdkInstallCommandParser.InstallPathOption);
    private readonly bool? _setDefaultInstall = result.GetValue(SdkInstallCommandParser.SetDefaultInstallOption);
    private readonly bool? _updateGlobalJson = result.GetValue(SdkInstallCommandParser.UpdateGlobalJsonOption);
    private readonly string? _manifestPath = result.GetValue(SdkInstallCommandParser.ManifestPathOption);
    private readonly bool _interactive = result.GetValue(SdkInstallCommandParser.InteractiveOption);
    private readonly bool _noProgress = result.GetValue(SdkInstallCommandParser.NoProgressOption);

    private readonly IDotnetInstallManager _dotnetInstaller = new DotnetInstallManager();
    private readonly ChannelVersionResolver _channelVersionResolver = new ChannelVersionResolver();

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
            ResolveChannelFromGlobalJson);

        var result = workflow.Execute(options);
        return result.ExitCode;
    }

    string? ResolveChannelFromGlobalJson(string globalJsonPath)
    {
        return Environment.GetEnvironmentVariable("DOTNET_TESTHOOK_GLOBALJSON_SDK_CHANNEL");
    }
}
