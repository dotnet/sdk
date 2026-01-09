// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Uninstall;

internal sealed class WorkloadUninstallCommandDefinition : WorkloadCommandDefinitionBase
{
    public readonly Argument<IEnumerable<string>> WorkloadIdArgument = CreateWorkloadIdArgument();
    public readonly Option<string> VersionOption = CreateSdkVersionOption();
    public override Option<bool> SkipSignCheckOption { get; } = CreateSkipSignCheckOption();
    public override Option<Utils.VerbosityOptions> VerbosityOption { get; } = CommonOptions.CreateVerbosityOption(Utils.VerbosityOptions.normal);

    public WorkloadUninstallCommandDefinition()
        : base("uninstall", CommandDefinitionStrings.WorkloadUninstallCommandDescription)
    {
        Arguments.Add(WorkloadIdArgument);
        Options.Add(SkipSignCheckOption);
        Options.Add(VerbosityOption);
    }
}
