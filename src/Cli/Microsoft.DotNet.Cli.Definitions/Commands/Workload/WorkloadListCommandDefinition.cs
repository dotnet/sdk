// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.List;

internal sealed class WorkloadListCommandDefinition : WorkloadCommandDefinitionBase
{
    // arguments are a list of workload to be detected
    public readonly Option<bool> MachineReadableOption = new("--machine-readable")
    {
        Hidden = true
    };

    public readonly Option<string> VersionOption = CreateSdkVersionOption();
    public override Option<Utils.VerbosityOptions> VerbosityOption { get; } = CommonOptions.CreateHiddenVerbosityOption();

    public override Option<string> TempDirOption { get; } = CreateTempDirOption().Hide();

    public readonly Option<bool> IncludePreviewsOption = new Option<bool>("--include-previews")
    {
        Description = CommandDefinitionStrings.IncludePreviewOptionDescription
    }.Hide();

    public override NuGetRestoreOptions RestoreOptions { get; } = new(hidden: true);

    public WorkloadListCommandDefinition()
        : base("list", CommandDefinitionStrings.WorkloadListCommandDescription)
    {
        Options.Add(MachineReadableOption);
        Options.Add(VerbosityOption);
        Options.Add(VersionOption);
        Options.Add(TempDirOption);
        Options.Add(IncludePreviewsOption);

        RestoreOptions.AddTo(Options);
    }
}
