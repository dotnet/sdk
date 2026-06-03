// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Runtime.ExceptionServices;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Update;

internal class RuntimeUpdateCommand(ParseResult result) : CommandBase(result)
{
    private readonly bool _noProgress = result.GetValue(CommonOptions.NoProgressOption);
    private readonly Verbosity _verbosity = result.GetValue(CommonOptions.VerbosityOption);
    private readonly string? _manifestPath = result.GetValue(CommonOptions.ManifestPathOption);
    private readonly string? _installPath = result.GetValue(CommonOptions.InstallPathOption);

    protected override string GetCommandName() => "runtime/update";

    protected override void ExecuteCore()
    {
        // Update all runtime-type components
        // (but not SDKs — those are updated via `dotnetup sdk update`)
        var workflow = new UpdateWorkflow(new ChannelVersionResolver());

        var components = OperatingSystem.IsWindows()
            ? [InstallComponent.Runtime, InstallComponent.ASPNETCore, InstallComponent.WindowsDesktop]
            : (InstallComponent[])[InstallComponent.Runtime, InstallComponent.ASPNETCore];

        // Try every component so a single failure doesn't mask updates to the others.
        // Capture the first failure and rethrow at the end so CommandBase can record
        // it via RecordException — that's what stamps error.type / error.category /
        // error.details on the command telemetry row. Subsequent failures hang off
        // the primary via AttachAdditionalFailure (not new telemetry rows) so
        // per-invocation failure-count KPIs aren't inflated; the total count for
        // the user-facing summary is derived from that list.
        ExceptionDispatchInfo? firstFailure = null;
        foreach (var component in components)
        {
            try
            {
                workflow.Execute(_manifestPath, _installPath, component, _noProgress, verbosity: _verbosity);
            }
            catch (DotnetInstallException ex)
            {
                AnsiConsole.MarkupLine(DotnetupTheme.Error($"Failed to update {component.GetDisplayName()}."));
                if (firstFailure is null)
                {
                    firstFailure = ExceptionDispatchInfo.Capture(ex);
                }
                else
                {
                    firstFailure.SourceException.AttachAdditionalFailure(ex);
                }
            }
        }

        if (firstFailure is not null)
        {
            int totalFailures = firstFailure.SourceException.GetAdditionalFailures().Count + 1;
            if (totalFailures > 1)
            {
                AnsiConsole.MarkupLine(DotnetupTheme.Error($"{totalFailures} of {components.Length} runtime components failed to update; reporting the first failure."));
            }
            firstFailure.Throw();
        }
    }
}
