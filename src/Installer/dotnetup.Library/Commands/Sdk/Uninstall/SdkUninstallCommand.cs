// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Uninstall;

internal class SdkUninstallCommand : CommandBase
{
    private readonly string _versionOrChannel;
    private readonly InstallSource _sourceFilter;
    private readonly string? _manifestPath;
    private readonly string? _installPath;

    public SdkUninstallCommand(ParseResult result, Argument<string?> channelArgument) : base(result)
    {
        _versionOrChannel = result.GetValue(channelArgument)!;
        _sourceFilter = result.GetValue(CommonOptions.SourceOption);
        _manifestPath = result.GetValue(CommonOptions.ManifestPathOption);
        _installPath = result.GetValue(CommonOptions.InstallPathOption);
    }

    protected override string GetCommandName() => "sdk/uninstall";

    protected override void ExecuteCore()
    {
        UninstallWorkflow.Execute(
            _manifestPath,
            _installPath,
            _versionOrChannel,
            _sourceFilter,
            InstallComponent.SDK);
    }
}
