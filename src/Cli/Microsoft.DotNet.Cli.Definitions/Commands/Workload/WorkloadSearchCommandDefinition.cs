// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Search;

internal sealed class WorkloadSearchCommandDefinition : WorkloadCommandDefinitionBase
{
    public readonly Argument<string> WorkloadIdStubArgument = new(CommandDefinitionStrings.WorkloadIdStubArgumentName)
    {
        Arity = ArgumentArity.ZeroOrOne,
        Description = CommandDefinitionStrings.WorkloadIdStubArgumentDescription
    };

    public readonly Option<string> VersionOption = CreateSdkVersionOption();
    public override Option<Utils.VerbosityOptions> VerbosityOption { get; } = CommonOptions.CreateHiddenVerbosityOption();

    public readonly WorkloadSearchVersionsCommandDefinition VersionCommand = new();

    public WorkloadSearchCommandDefinition()
        : base("search", CommandDefinitionStrings.WorkloadSearchCommandDescription)
    {
        Subcommands.Add(VersionCommand);
        Arguments.Add(WorkloadIdStubArgument);
        Options.Add(VerbosityOption);
        Options.Add(VersionOption);
    }
}
