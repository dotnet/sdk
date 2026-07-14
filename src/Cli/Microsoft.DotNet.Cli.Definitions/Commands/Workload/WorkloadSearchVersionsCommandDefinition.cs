// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Search;

internal sealed class WorkloadSearchVersionsCommandDefinition : WorkloadCommandDefinitionBase
{
    public readonly Argument<IEnumerable<string>> WorkloadVersionArgument = new(CommandDefinitionStrings.WorkloadVersionArgument)
    {
        Arity = ArgumentArity.ZeroOrMore,
        Description = CommandDefinitionStrings.WorkloadVersionArgumentDescription
    };

    public readonly Option<int> TakeOption = new("--take")
    {
        DefaultValueFactory = (_) => 5
    };

    public readonly Option<string> FormatOption = new("--format")
    {
        Description = CommandDefinitionStrings.FormatOptionDescription
    };

    public readonly Option<bool> IncludePreviewsOption = new("--include-previews");

    public WorkloadSearchVersionsCommandDefinition()
        : base("version", CommandDefinitionStrings.PrintSetVersionsDescription)
    {
        Arguments.Add(WorkloadVersionArgument);
        Options.Add(FormatOption);
        Options.Add(TakeOption);
        Options.Add(IncludePreviewsOption);

        TakeOption.Validators.Add(optionResult =>
        {
            if (optionResult.GetValueOrDefault<int>() <= 0)
            {
                throw new ArgumentException("The --take option must be positive.");
            }
        });

        Validators.Add(result =>
        {
            if (result.GetValue(Parent.WorkloadIdStubArgument) != null)
            {
                result.AddError(string.Format(CommandDefinitionStrings.CannotCombineSearchStringAndVersion, Parent.WorkloadIdStubArgument.Name, Name));
            }
        });

        Validators.Add(result =>
        {
            var versionArgument = result.GetValue(WorkloadVersionArgument);
            if (versionArgument is not null && !versionArgument.All(v => v.Contains('@')) && !WorkloadSetVersion.IsWorkloadSetPackageVersion(versionArgument.SingleOrDefault(defaultValue: string.Empty)))
            {
                result.AddError(string.Format(CommandDefinitionStrings.UnrecognizedCommandOrArgument, string.Join(' ', versionArgument)));
            }
        });
    }

    public WorkloadSearchCommandDefinition Parent
        => (WorkloadSearchCommandDefinition)Parents.Single();
}
