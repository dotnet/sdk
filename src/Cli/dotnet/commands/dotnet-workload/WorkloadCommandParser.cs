// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.TemplateEngine.Cli.Commands;
using CommonStrings = Microsoft.DotNet.Workloads.Workload.LocalizableStrings;
using IReporter = Microsoft.DotNet.Cli.Utils.IReporter;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-workload";

        private static readonly CliCommand Command = ConstructCommand();

        public static readonly CliOption<bool> InfoOption = new("--info")
        {
            Description = CommonStrings.WorkloadInfoDescription
        };

        public static readonly CliOption<bool> VersionOption = new("--version")
        {
            Description = CommonStrings.WorkloadVersionDescription
        };

        public static CliCommand GetCommand()
        {
            Command.Options.Add(InfoOption);
            Command.Options.Add(VersionOption);
            return Command;
        }

        internal static string GetWorkloadsVersion(WorkloadInfoHelper workloadInfoHelper = null)
        {
            workloadInfoHelper ??= new WorkloadInfoHelper(false);

            return workloadInfoHelper.ManifestProvider.GetWorkloadVersion();
        }

        internal static void ShowWorkloadsInfo(ParseResult parseResult = null, WorkloadInfoHelper workloadInfoHelper = null, IReporter reporter = null, string dotnetDir = null)
        {
            workloadInfoHelper ??= new WorkloadInfoHelper(parseResult != null ? parseResult.HasOption(SharedOptions.InteractiveOption) : false);
            IEnumerable<WorkloadId> installedList = workloadInfoHelper.InstalledSdkWorkloadIds;
            InstalledWorkloadsCollection installedWorkloads = workloadInfoHelper.AddInstalledVsWorkloads(installedList);
            reporter ??= Cli.Utils.Reporter.Output;
            string dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);

            reporter.WriteLine($" Workload version: {workloadInfoHelper.ManifestProvider.GetWorkloadVersion()}");

            if (installedWorkloads.Count == 0)
            {
                reporter.WriteLine(CommonStrings.NoWorkloadsInstalledInfoWarning);
                return;
            }

            var manifestInfoDict = workloadInfoHelper.WorkloadResolver.GetInstalledManifests().ToDictionary(info => info.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var workload in installedWorkloads.AsEnumerable())
            {
                var workloadManifest = workloadInfoHelper.WorkloadResolver.GetManifestFromWorkload(new WorkloadId(workload.Key));
                var workloadFeatureBand = manifestInfoDict[workloadManifest.Id].ManifestFeatureBand;

                const int align = 10;
                const string separator = "   ";

                reporter.WriteLine($" [{workload.Key}]");

                reporter.Write($"{separator}{CommonStrings.WorkloadSourceColumn}:");
                reporter.WriteLine($" {workload.Value,align}");

                reporter.Write($"{separator}{CommonStrings.WorkloadManfiestVersionColumn}:");
                reporter.WriteLine($"    {workloadManifest.Version + '/' + workloadFeatureBand,align}");

                reporter.Write($"{separator}{CommonStrings.WorkloadManifestPathColumn}:");
                reporter.WriteLine($"       {workloadManifest.ManifestPath,align}");

                reporter.Write($"{separator}{CommonStrings.WorkloadInstallTypeColumn}:");
                reporter.WriteLine($"       {WorkloadInstallType.GetWorkloadInstallType(new SdkFeatureBand(workloadFeatureBand), dotnetPath).ToString(),align}"
                );
                reporter.WriteLine("");
            }
        }

        private static int ProcessArgs(ParseResult parseResult)
        {
            if (parseResult.HasOption(InfoOption) && parseResult.RootSubCommandResult() == "workload")
            {
                ShowWorkloadsInfo(parseResult);
                Reporter.Output.WriteLine(string.Empty);
                return 0;
            }
            else if (parseResult.HasOption(VersionOption) && parseResult.RootSubCommandResult() == "workload")
            {
                Reporter.Output.WriteLine(GetWorkloadsVersion());
                Reporter.Output.WriteLine(string.Empty);
                return 0;
            }
            return parseResult.HandleMissingCommand();
        }

        private static CliCommand ConstructCommand()
        {
            DocumentedCommand command = new("workload", DocsLink, CommonStrings.CommandDescription);

            command.Subcommands.Add(WorkloadInstallCommandParser.GetCommand());
            command.Subcommands.Add(WorkloadUpdateCommandParser.GetCommand());
            command.Subcommands.Add(WorkloadListCommandParser.GetCommand());
            command.Subcommands.Add(WorkloadSearchCommandParser.GetCommand());
            command.Subcommands.Add(WorkloadUninstallCommandParser.GetCommand());
            command.Subcommands.Add(WorkloadRepairCommandParser.GetCommand());
            command.Subcommands.Add(WorkloadRestoreCommandParser.GetCommand());
            command.Subcommands.Add(WorkloadCleanCommandParser.GetCommand());
            command.Subcommands.Add(WorkloadElevateCommandParser.GetCommand());

            command.Validators.Add(commandResult =>
            {
                if (commandResult.GetResult(InfoOption) is null && commandResult.GetResult(VersionOption) is null && !commandResult.Children.Any(child => child is System.CommandLine.Parsing.CommandResult))
                {
                    commandResult.AddError(Tools.CommonLocalizableStrings.RequiredCommandNotPassed);
                }
            });

            command.SetAction(ProcessArgs);

            return command;
        }
    }
}
