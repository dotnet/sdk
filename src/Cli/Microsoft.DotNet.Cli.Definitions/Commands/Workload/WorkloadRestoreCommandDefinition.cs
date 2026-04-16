// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Restore;

internal sealed class WorkloadRestoreCommandDefinition : InstallingWorkloadCommandDefinition
{
    public readonly Argument<IEnumerable<string>> SlnOrProjectArgument = new(CommandDefinitionStrings.SolutionOrProjectArgumentName)
    {
        Description = CommandDefinitionStrings.SolutionOrProjectArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public readonly Option<bool> SkipManifestUpdateOption = CreateSkipManifestUpdateOption();

    public WorkloadRestoreCommandDefinition()
        : base("restore", CommandDefinitionStrings.WorkloadRestoreCommandDescription)
    {
        Arguments.Add(SlnOrProjectArgument);
        Options.Add(SkipManifestUpdateOption);
    }
}
