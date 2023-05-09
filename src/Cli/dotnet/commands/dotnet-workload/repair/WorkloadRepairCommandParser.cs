// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Workloads.Workload.Repair;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Repair.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadRepairCommandParser
    {
        public static readonly Option<string> ConfigOption = InstallingWorkloadCommandParser.ConfigOption;

        public static readonly Option<string[]> SourceOption = InstallingWorkloadCommandParser.SourceOption;

        public static readonly Option<string> VersionOption = InstallingWorkloadCommandParser.VersionOption;

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("repair", LocalizableStrings.CommandDescription);

            command.AddOption(VersionOption);
            command.AddOption(ConfigOption);
            command.AddOption(SourceOption);
            command.AddOption(CommonOptions.VerbosityOption);
            command.AddWorkloadCommandNuGetRestoreActionConfigOptions();
            command.AddOption(WorkloadInstallCommandParser.SkipSignCheckOption);

            command.SetHandler((parseResult) => new WorkloadRepairCommand(parseResult).Execute());

            return command;
        }
    }
}
