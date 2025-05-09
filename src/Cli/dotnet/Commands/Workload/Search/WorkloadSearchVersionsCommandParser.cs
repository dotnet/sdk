// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Search;

internal static class WorkloadSearchVersionsCommandParser
{
    public static readonly Argument<IEnumerable<string>> WorkloadVersionArgument =
        new(CliCommandStrings.WorkloadVersionArgument)
        {
            Arity = ArgumentArity.ZeroOrMore,
            Description = CliCommandStrings.WorkloadVersionArgumentDescription
        };

    public static readonly Option<int> TakeOption = new("--take") { DefaultValueFactory = (_) => 5 };

    public static readonly Option<string> FormatOption = new("--format")
    {
        Description = CliCommandStrings.FormatOptionDescription
    };

    public static readonly Option<bool> IncludePreviewsOption = new("--include-previews");

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var command = new Command("version", CliCommandStrings.PrintSetVersionsDescription);
        command.Arguments.Add(WorkloadVersionArgument);
        command.Options.Add(FormatOption);
        command.Options.Add(TakeOption);
        command.Options.Add(IncludePreviewsOption);

        TakeOption.Validators.Add(optionResult =>
        {
            if (optionResult.GetValueOrDefault<int>() <= 0)
            {
                throw new ArgumentException("The --take option must be positive.");
            }
        });

        command.Validators.Add(result =>
        {
            if (result.GetValue(WorkloadSearchCommandParser.WorkloadIdStubArgument) != null)
            {
                result.AddError(string.Format(CliCommandStrings.CannotCombineSearchStringAndVersion, WorkloadSearchCommandParser.WorkloadIdStubArgument.Name, command.Name));
            }
        });

        command.Validators.Add(result =>
        {
            var versionArgument = result.GetValue(WorkloadVersionArgument);
            if (versionArgument is not null && !versionArgument.All(v => v.Contains('@')) && !WorkloadSetVersion.IsWorkloadSetPackageVersion(versionArgument.SingleOrDefault(defaultValue: string.Empty)))
            {
                result.AddError(string.Format(CliStrings.UnrecognizedCommandOrArgument, string.Join(' ', versionArgument)));
            }
        });

        command.SetAction(parseResult => new WorkloadSearchVersionsCommand(parseResult).Execute());

        return command;
    }
}
