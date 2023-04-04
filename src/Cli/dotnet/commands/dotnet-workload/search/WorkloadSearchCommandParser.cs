// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Workloads.Workload.Search;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Search.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadSearchCommandParser
    {
        public static readonly CliArgument<string> WorkloadIdStubArgument =
            new CliArgument<string>(LocalizableStrings.WorkloadIdStubArgumentName)
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
            command.Options.Add(CommonOptions.HiddenVerbosityOption);
            command.Options.Add(VersionOption);

            command.SetAction((parseResult) => new WorkloadSearchCommand(parseResult).Execute());

            return command;
        }
    }
}
