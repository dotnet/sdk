// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Workloads.Workload.Search;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Search.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class SearchWorkloadSetsParser
    {
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
            command.Options.Add(FormatOption);
            command.Options.Add(TakeOption);

            TakeOption.Validators.Add(optionResult =>
            {
                if (optionResult.GetValueOrDefault<int>() <= 0)
                {
                    throw new ArgumentException(LocalizableStrings.TakeOptionMustBePositive);
                }
            });

            command.SetAction(parseResult => new WorkloadSearchCommand(parseResult)
            {
                ListWorkloadSetVersions = true
            }.Execute());

            return command;
        }
    }
}
