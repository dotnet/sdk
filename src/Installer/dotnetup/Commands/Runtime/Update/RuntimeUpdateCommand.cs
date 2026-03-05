// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Update;

internal class RuntimeUpdateCommand(ParseResult result) : CommandBase(result)
{
    private readonly string? _componentSpec = result.GetValue(RuntimeUpdateCommandParser.ComponentSpecArgument);
    private readonly bool _noProgress = result.GetValue(RuntimeUpdateCommandParser.NoProgressOption);
    private readonly string? _manifestPath = result.GetValue(RuntimeUpdateCommandParser.ManifestPathOption);
    private readonly string? _installPath = result.GetValue(RuntimeUpdateCommandParser.InstallPathOption);

    protected override string GetCommandName() => "runtime/update";

    protected override int ExecuteCore()
    {
        // Parse the optional component spec to determine which runtime(s) to update
        InstallComponent? componentFilter = null;

        if (!string.IsNullOrWhiteSpace(_componentSpec))
        {
            var (component, _, errorMessage) = Runtime.Install.RuntimeInstallCommand.ParseComponentSpec(_componentSpec);

            if (errorMessage != null)
            {
                Console.Error.WriteLine(errorMessage);
                RecordFailure("invalid_component_spec", category: "user");
                return 1;
            }

            componentFilter = component;
        }

        // If no specific runtime type is specified, update all runtime-type components
        // (but not SDKs — those are updated via `dotnetup sdk update`)
        var workflow = new UpdateWorkflow(new ChannelVersionResolver());

        if (componentFilter is not null)
        {
            return workflow.Execute(_manifestPath, _installPath, componentFilter, _noProgress);
        }

        // Update all runtime components
        int exitCode = 0;
        foreach (var component in new[] { InstallComponent.Runtime, InstallComponent.ASPNETCore, InstallComponent.WindowsDesktop })
        {
            int result = workflow.Execute(_manifestPath, _installPath, component, _noProgress);
            if (result != 0)
            {
                exitCode = result;
            }
        }

        return exitCode;
    }
}
