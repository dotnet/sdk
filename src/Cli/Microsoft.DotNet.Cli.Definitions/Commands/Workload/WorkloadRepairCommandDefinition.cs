// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Repair;

internal sealed class WorkloadRepairCommandDefinition : WorkloadCommandDefinitionBase
{
    public readonly Option<string> ConfigOption = CreateConfigOption();

    public readonly Option<string[]> SourceOption = CreateSourceOption();

    public readonly Option<string> SdkVersionOption = CreateSdkVersionOption();

    public override Option<Utils.VerbosityOptions> VerbosityOption { get; } = CommonOptions.CreateVerbosityOption(Utils.VerbosityOptions.normal);

    public override Option<bool> SkipSignCheckOption { get; } = CreateSkipSignCheckOption();

    public override NuGetRestoreOptions RestoreOptions { get; } = new();

    public WorkloadRepairCommandDefinition()
        : base("repair", CommandDefinitionStrings.WorkloadRepairCommandDescription)
    {
        Options.Add(SdkVersionOption);
        Options.Add(ConfigOption);
        Options.Add(SourceOption);
        Options.Add(VerbosityOption);
        RestoreOptions.AddTo(Options);
        Options.Add(SkipSignCheckOption);
    }
}
