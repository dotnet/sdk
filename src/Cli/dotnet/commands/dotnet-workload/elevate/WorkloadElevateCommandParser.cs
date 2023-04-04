// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Workloads.Workload.Elevate;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Elevate.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadElevateCommandParser
    {
        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("elevate", LocalizableStrings.CommandDescription)
            {
                Hidden = true
            };

            command.SetAction((parseResult) => new WorkloadElevateCommand(parseResult).Execute());

            return command;
        }
    }
}
