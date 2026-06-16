// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Workloads.Workload.History;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadHistoryCommandParser
    {
        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var command = new CliCommand("history", LocalizableStrings.CommandDescription);

            command.SetAction(parseResult => new WorkloadHistoryCommand(parseResult).Execute());

            return command;
        }
    }
}