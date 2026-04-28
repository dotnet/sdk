// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Uninstall;

internal class SdkUninstallCommand(ParseResult result) : CommandBase(result)
{
    private readonly string _versionOrChannel = result.GetValue(SdkUninstallCommandParser.ChannelArgument)!;
    private readonly InstallSource _sourceFilter = result.GetValue(CommonOptions.SourceOption);
    private readonly string? _manifestPath = result.GetValue(CommonOptions.ManifestPathOption);
    private readonly string? _installPath = result.GetValue(CommonOptions.InstallPathOption);

    protected override string GetCommandName() => "sdk/uninstall";

    protected override int ExecuteCore()
    {
        return UninstallWorkflow.Execute(
            _manifestPath,
            _installPath,
            _versionOrChannel,
            _sourceFilter,
            InstallComponent.SDK);
    }
}
