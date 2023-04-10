// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.CommandLine;
using Microsoft.DotNet.Workloads.Workload.Clean;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Clean.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadCleanCommandParser
    {
        public static readonly CliOption<bool> CleanAllOption = new("--all") { Description = LocalizableStrings.CleanAllOptionDescription };

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("clean", LocalizableStrings.CommandDescription);

            command.Options.Add(CleanAllOption);

            command.SetAction((parseResult) => new WorkloadCleanCommand(parseResult).Execute());

            return command;
        }
    }
}
