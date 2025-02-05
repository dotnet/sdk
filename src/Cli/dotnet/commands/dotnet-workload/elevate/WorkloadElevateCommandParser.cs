// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Workloads.Workload.Elevate;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Elevate.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadElevateCommandParser
    {
        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new("elevate", LocalizableStrings.CommandDescription)
            {
                Hidden = true
            };

            command.SetAction((parseResult) => new WorkloadElevateCommand(parseResult).Execute());

            return command;
        }
    }
}
