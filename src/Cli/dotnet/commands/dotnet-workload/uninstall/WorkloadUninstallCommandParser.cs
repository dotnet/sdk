// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Workloads.Workload.Uninstall;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Uninstall.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadUninstallCommandParser
    {
        public static readonly Argument<IEnumerable<string>> WorkloadIdArgument = WorkloadInstallCommandParser.WorkloadIdArgument;

        public static readonly Option<string> VersionOption = InstallingWorkloadCommandParser.VersionOption;

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new("uninstall", LocalizableStrings.CommandDescription);
            command.Arguments.Add(WorkloadIdArgument);
            command.Options.Add(WorkloadInstallCommandParser.SkipSignCheckOption);
            command.Options.Add(CommonOptions.VerbosityOption);

            command.SetAction((parseResult) => new WorkloadUninstallCommand(parseResult).Execute());

            return command;
        }
    }
}
