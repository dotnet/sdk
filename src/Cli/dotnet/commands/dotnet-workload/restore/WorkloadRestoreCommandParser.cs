// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Workloads.Workload.Restore;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Restore.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadRestoreCommandParser
    {
        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("restore", LocalizableStrings.CommandDescription);

            command.Arguments.Add(RestoreCommandParser.SlnOrProjectArgument);
            WorkloadInstallCommandParser.AddWorkloadInstallCommandOptions(command);

            command.SetAction((parseResult) => new WorkloadRestoreCommand(parseResult).Execute());

            return command;
        }
    }
}
