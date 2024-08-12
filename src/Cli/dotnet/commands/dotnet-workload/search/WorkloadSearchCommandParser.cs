// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Workloads.Workload.Search;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Search.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadSearchCommandParser
    {
        public static readonly CliOption<bool> SetVersionsOption = new("version")
        {
            Description = LocalizableStrings.PrintSetVersionsDescription
        };

        public static readonly CliOption<int> TakeOption = new("--take") { Hidden = true };

        public static readonly CliOption<string> FormatOption = new("--format")
        {
            Description = LocalizableStrings.FormatOptionDescription
        };

        public static readonly CliArgument<string> WorkloadIdStubArgument =
            new(LocalizableStrings.WorkloadIdStubArgumentName)
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = LocalizableStrings.WorkloadIdStubArgumentDescription
            };

        public static readonly CliOption<string> VersionOption = InstallingWorkloadCommandParser.VersionOption;

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var command = new CliCommand("search", LocalizableStrings.CommandDescription);
            command.Arguments.Add(WorkloadIdStubArgument);
            command.Options.Add(SetVersionsOption);
            command.Options.Add(TakeOption);
            command.Options.Add(FormatOption);
            command.Options.Add(CommonOptions.HiddenVerbosityOption);
            command.Options.Add(VersionOption);

            command.SetAction((parseResult) => new WorkloadSearchCommand(parseResult).Execute());

            return command;
        }
    }
}
