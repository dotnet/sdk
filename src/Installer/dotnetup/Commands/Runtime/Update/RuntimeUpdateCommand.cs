// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Update;

internal class RuntimeUpdateCommand(ParseResult result) : CommandBase(result)
{
    private readonly bool _noProgress = result.GetValue(CommonOptions.NoProgressOption);
    private readonly string? _manifestPath = result.GetValue(CommonOptions.ManifestPathOption);
    private readonly string? _installPath = result.GetValue(CommonOptions.InstallPathOption);

    protected override string GetCommandName() => "runtime/update";

    protected override int ExecuteCore()
    {
        // Update all runtime-type components
        // (but not SDKs — those are updated via `dotnetup sdk update`)
        var workflow = new UpdateWorkflow(new ChannelVersionResolver());

        var components = OperatingSystem.IsWindows()
            ? [InstallComponent.Runtime, InstallComponent.ASPNETCore, InstallComponent.WindowsDesktop]
            : (InstallComponent[])[InstallComponent.Runtime, InstallComponent.ASPNETCore];

        int exitCode = 0;
        foreach (var component in components)
        {
            int componentExitCode = workflow.Execute(_manifestPath, _installPath, component, _noProgress);
            if (componentExitCode != 0)
            {
                exitCode = componentExitCode;
            }
        }

        return exitCode;
    }
}
