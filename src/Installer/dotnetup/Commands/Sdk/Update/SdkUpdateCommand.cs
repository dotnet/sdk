// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Update;

internal class SdkUpdateCommand(ParseResult result, bool updateAllOverride = false) : CommandBase(result)
{
    private readonly bool _updateAll = updateAllOverride || result.GetValue(SdkUpdateCommandParser.UpdateAllOption);
    private readonly bool _updateGlobalJson = result.GetValue(SdkUpdateCommandParser.UpdateGlobalJsonOption);
    private readonly bool _noProgress = result.GetValue(CommonOptions.NoProgressOption);
    private readonly string? _manifestPath = result.GetValue(CommonOptions.ManifestPathOption);
    private readonly string? _installPath = result.GetValue(CommonOptions.InstallPathOption);

    protected override string GetCommandName() => "sdk/update";

    protected override int ExecuteCore()
    {
        var workflow = new UpdateWorkflow(new ChannelVersionResolver());
        return workflow.Execute(
            _manifestPath,
            _installPath,
            _updateAll ? null : InstallComponent.SDK,
            _noProgress,
            _updateGlobalJson);
    }
}
