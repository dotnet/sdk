// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install;

internal sealed class WorkloadInstallCommandDefinition : InstallingWorkloadCommandDefinition
{
    public readonly Argument<IEnumerable<string>> WorkloadIdArgument = CreateWorkloadIdArgument();
    public readonly Option<bool> SkipManifestUpdateOption = CreateSkipManifestUpdateOption();

    public WorkloadInstallCommandDefinition()
        : base("install", CommandDefinitionStrings.WorkloadInstallCommandDescription)
    {
        Arguments.Add(WorkloadIdArgument);
        Options.Add(SkipManifestUpdateOption);
    }
}
