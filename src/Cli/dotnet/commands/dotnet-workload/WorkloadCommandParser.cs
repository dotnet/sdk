// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using CommonStrings = Microsoft.DotNet.Workloads.Workload.LocalizableStrings;
using IReporter = Microsoft.DotNet.Cli.Utils.IReporter;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-workload";

        private static readonly Command Command = ConstructCommand();

        public static readonly CliOption<bool> InfoOption = new CliOption<bool>("--info", CommonStrings.WorkloadInfoDescription);

        public static Command GetCommand()
        {
            Command.Options.Add(InfoOption);
            return Command;
        }

        internal static void ShowWorkloadsInfo(IWorkloadInfoHelper workloadInfoHelper = null, IReporter reporter = null)
        {
            workloadInfoHelper ??= new WorkloadInfoHelper();
            IEnumerable<WorkloadId> installedList = workloadInfoHelper.InstalledSdkWorkloadIds;
            InstalledWorkloadsCollection installedWorkloads = workloadInfoHelper.AddInstalledVsWorkloads(installedList);
            reporter ??= Cli.Utils.Reporter.Output;

            if (!installedList.Any())
            {
                reporter.WriteLine(CommonStrings.NoWorkloadsInstalledInfoWarning);
                return;
            }


            foreach (var workload in installedWorkloads.AsEnumerable())
            {
                var workloadManifest = workloadInfoHelper.WorkloadResolver.GetManifestFromWorkload(new WorkloadId(workload.Key));
                var workloadFeatureBand = new WorkloadManifestInfo(
                    workloadManifest.Id,
                    workloadManifest.Version,
                    Path.GetDirectoryName(workloadManifest.ManifestPath)!).ManifestFeatureBand;

                const int align = 10;
                const string separator = "   ";

                reporter.WriteLine($" {'[' + workload.Key + ']'}");

                reporter.Write($"{separator}{CommonStrings.WorkloadSourceColumn}:");
                reporter.WriteLine($" {workload.Value,align}");

                reporter.Write($"{separator}{CommonStrings.WorkloadManfiestVersionColumn}:");
                reporter.WriteLine($"    {workloadManifest.Version + '/' + workloadFeatureBand,align}");

                reporter.Write($"{separator}{CommonStrings.WorkloadManifestPathColumn}:");
                reporter.WriteLine($"       {workloadManifest.ManifestPath,align}");

                reporter.Write($"{separator}{CommonStrings.WorkloadInstallTypeColumn}:");
                reporter.WriteLine($"       {WorkloadInstallerFactory.GetWorkloadInstallType(new SdkFeatureBand(workloadFeatureBand), workloadManifest.ManifestPath).ToString(),align}"
                );
                reporter.WriteLine("");
            }
        }

        private static int ProcessArgs(ParseResult parseResult)
        {
            if (parseResult.HasOption(InfoOption) && parseResult.RootSubCommandResult() == "workload")
            {
                ShowWorkloadsInfo();
                return 0;
            }
            return parseResult.HandleMissingCommand();
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("workload", DocsLink, CommonStrings.CommandDescription);

            command.Subcommands.Add(WorkloadInstallCommandParser.GetCommand());
            command.Subcommands.Add(WorkloadUpdateCommandParser.GetCommand());
            command.Subcommands.Add(WorkloadListCommandParser.GetCommand());
            command.Subcommands.Add(WorkloadSearchCommandParser.GetCommand());
            command.Subcommands.Add(WorkloadUninstallCommandParser.GetCommand());
            command.Subcommands.Add(WorkloadRepairCommandParser.GetCommand());
            command.Subcommands.Add(WorkloadRestoreCommandParser.GetCommand());
            command.Subcommands.Add(WorkloadElevateCommandParser.GetCommand());

            command.SetHandler((parseResult) => ProcessArgs(parseResult));

            return command;
        }
    }
}
