// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;

internal class SdkInstallCommand : InstallCommand
{
    private readonly string[] _channels;

    public SdkInstallCommand(ParseResult result, Argument<string[]>? channelArgument = null) : base(result)
    {
        _channels = channelArgument is not null
            ? result.GetValue(channelArgument) ?? []
            : [];
        UpdateGlobalJson = result.GetValue(SdkInstallCommandParser.UpdateGlobalJsonOption) ?? false;
    }

    public override bool UpdateGlobalJson { get; }
    public override IReadOnlyCollection<InstallComponent> MigrationComponents => [InstallComponent.SDK];

    protected override string GetCommandName() => "sdk/install";

    protected override void ExecuteCore()
    {
        // Map each channel to a MinimalInstallSpec. If none provided, a single null-channel
        // entry lets the workflow fall back to global.json or "latest".
        var specs = _channels.Length > 0
            ? _channels.Select(c => new MinimalInstallSpec(InstallComponent.SDK, c)).ToArray()
            : [new MinimalInstallSpec(InstallComponent.SDK, null)];

        var workflow = new InstallWorkflow(this);
        workflow.Execute(specs);
    }
}
