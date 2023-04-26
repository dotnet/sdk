// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.Collections.Generic;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Uninstall.LocalizableStrings;
using Microsoft.DotNet.Workloads.Workload.Uninstall;
using Microsoft.DotNet.Workloads.Workload;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadUninstallCommandParser
    {
        public static readonly CliArgument<IEnumerable<string>> WorkloadIdArgument = WorkloadInstallCommandParser.WorkloadIdArgument;

        public static readonly CliOption<string> VersionOption = InstallingWorkloadCommandParser.VersionOption;

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("uninstall", LocalizableStrings.CommandDescription);
            command.Arguments.Add(WorkloadIdArgument);
            command.Options.Add(WorkloadInstallCommandParser.SkipSignCheckOption);
            command.Options.Add(CommonOptions.VerbosityOption);

            command.SetAction((parseResult) => new WorkloadUninstallCommand(parseResult).Execute());

            return command;
        }
    }
}
