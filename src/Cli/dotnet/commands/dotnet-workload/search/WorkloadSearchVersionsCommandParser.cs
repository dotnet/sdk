// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Workloads.Workload.Search;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Search.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadSearchVersionsCommandParser
    {
        public static readonly CliArgument<string> WorkloadVersionArgument =
            new(LocalizableStrings.WorkloadVersionArgument)
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = LocalizableStrings.WorkloadVersionArgumentDescription
            };

        public static readonly CliOption<int> TakeOption = new("--take") { DefaultValueFactory = (_) => 5 };

        public static readonly CliOption<string> FormatOption = new("--format")
        {
            Description = LocalizableStrings.FormatOptionDescription
        };

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var command = new CliCommand("version", LocalizableStrings.PrintSetVersionsDescription);
            command.Arguments.Add(WorkloadVersionArgument);
            command.Options.Add(FormatOption);
            command.Options.Add(TakeOption);

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
                    result.AddError(string.Format(LocalizableStrings.CannotCombineSearchStringAndVersion, WorkloadSearchCommandParser.WorkloadIdStubArgument.Name, command.Name));
                }
            });

            command.Validators.Add(result =>
            {
                var versionArgument = result.GetValue(WorkloadVersionArgument);
                if (versionArgument is not null)
                {
                    var coreComponents = versionArgument.Split(['-', '+'], 2)[0].Split('.');
                    if (coreComponents.Length != 3 && coreComponents.Length != 4)
                    {
                        result.AddError(string.Format(CommandLineValidation.LocalizableStrings.UnrecognizedCommandOrArgument, versionArgument));
                    }
                }
            });

            command.SetAction(parseResult => new WorkloadSearchVersionsCommand(parseResult).Execute());

            return command;
        }
    }
}
