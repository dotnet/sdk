// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;

internal class SdkInstallCommand(ParseResult result) : InstallCommand(result)
{
    private readonly string[] _channels = result.GetValue(SdkInstallCommandParser.ChannelArguments) ?? [];

    public override bool UpdateGlobalJson { get; } = result.GetValue(SdkInstallCommandParser.UpdateGlobalJsonOption) ?? false;

    protected override string GetCommandName() => "sdk/install";

    protected override int ExecuteCore()
    {
        // Map each channel to a MinimalInstallSpec. If none provided, a single null-channel
        // entry lets the workflow fall back to global.json or "latest".
        var specs = _channels.Length > 0
            ? _channels.Select(c => new MinimalInstallSpec(InstallComponent.SDK, c)).ToArray()
            : [new MinimalInstallSpec(InstallComponent.SDK, null)];

        var workflow = new InstallWorkflow(this);
        workflow.Execute(specs);
        return 0;
    }
}
