// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Workload.Clean;
using Microsoft.DotNet.Cli.Commands.Workload.Config;
using Microsoft.DotNet.Cli.Commands.Workload.Elevate;
using Microsoft.DotNet.Cli.Commands.Workload.History;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Commands.Workload.List;
using Microsoft.DotNet.Cli.Commands.Workload.Repair;
using Microsoft.DotNet.Cli.Commands.Workload.Restore;
using Microsoft.DotNet.Cli.Commands.Workload.Search;
using Microsoft.DotNet.Cli.Commands.Workload.Uninstall;
using Microsoft.DotNet.Cli.Commands.Workload.Update;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.TemplateEngine.Cli.Commands;
using IReporter = Microsoft.DotNet.Cli.Utils.IReporter;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli.Commands.Workload;

internal static class WorkloadCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-workload";

    public static readonly Option<bool> InfoOption = new("--info")
    {
        Description = CliCommandStrings.WorkloadInfoDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<bool> VersionOption = new("--version")
    {
        Description = CliCommandStrings.WorkloadVersionDescription,
        Arity = ArgumentArity.Zero
    };

    public static Command GetCommand()
    {
        return Command;
    }

    private static readonly Command Command = ConstructCommand();

    internal static string GetWorkloadsVersion(WorkloadInfoHelper workloadInfoHelper = null)
    {
        workloadInfoHelper ??= new WorkloadInfoHelper(false);

        var versionInfo = workloadInfoHelper.ManifestProvider.GetWorkloadVersion();

        // The explicit space here is intentional, as it's easy to miss in localization and crucial for parsing
        return versionInfo.Version + (versionInfo.IsInstalled ? string.Empty : ' ' + CliCommandStrings.WorkloadVersionNotInstalledShort);
    }

    internal static void ShowWorkloadsInfo(ParseResult parseResult = null, WorkloadInfoHelper workloadInfoHelper = null, IReporter reporter = null, string dotnetDir = null, bool showVersion = true)
    {
        workloadInfoHelper ??= new WorkloadInfoHelper(parseResult != null ? parseResult.HasOption(SharedOptions.InteractiveOption) : false);
        reporter ??= Reporter.Output;
        var versionInfo = workloadInfoHelper.ManifestProvider.GetWorkloadVersion();

        void WriteUpdateModeAndAnyError(string indent = "")
        {
            var useWorkloadSets = InstallStateContents.FromPath(Path.Combine(WorkloadInstallType.GetInstallStateFolder(workloadInfoHelper._currentSdkFeatureBand, workloadInfoHelper.UserLocalPath), "default.json")).ShouldUseWorkloadSets();
            var workloadSetsString = useWorkloadSets ? "workload sets" : "loose manifests";
            reporter.WriteLine(indent + string.Format(CliCommandStrings.WorkloadManifestInstallationConfiguration, workloadSetsString));

            if (!versionInfo.IsInstalled)
            {
                reporter.WriteLine(indent + string.Format(CliCommandStrings.WorkloadSetFromGlobalJsonNotInstalled, versionInfo.Version, versionInfo.GlobalJsonPath));
            }
            else if (versionInfo.WorkloadSetsEnabledWithoutWorkloadSet)
            {
                reporter.WriteLine(indent + CliCommandStrings.ShouldInstallAWorkloadSet);
            }
        }

        if (showVersion)
        {
            reporter.WriteLine($" Workload version: {GetWorkloadsVersion()}");

            WriteUpdateModeAndAnyError(indent: " ");
            reporter.WriteLine();
        }

        if (versionInfo.IsInstalled)
        {
            IEnumerable<WorkloadId> installedList = workloadInfoHelper.InstalledSdkWorkloadIds;
            InstalledWorkloadsCollection installedWorkloads = workloadInfoHelper.AddInstalledVsWorkloads(installedList);
            string dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);

            if (installedWorkloads.Count == 0)
            {
                reporter.WriteLine(CliCommandStrings.NoWorkloadsInstalledInfoWarning);
            }
            else
            {
                var manifestInfoDict = workloadInfoHelper.WorkloadResolver.GetInstalledManifests().ToDictionary(info => info.Id, StringComparer.OrdinalIgnoreCase);

                foreach (var workload in installedWorkloads.AsEnumerable())
                {
                    var workloadManifest = workloadInfoHelper.WorkloadResolver.GetManifestFromWorkload(new WorkloadId(workload.Key));
                    var workloadFeatureBand = manifestInfoDict[workloadManifest.Id].ManifestFeatureBand;

                    const int align = 10;
                    const string separator = "   ";

                    reporter.WriteLine($" [{workload.Key}]");

                    reporter.Write($"{separator}{CliCommandStrings.WorkloadSourceColumn}:");
                    reporter.WriteLine($" {workload.Value,align}");

                    reporter.Write($"{separator}{CliCommandStrings.WorkloadManifestVersionColumn}:");
                    reporter.WriteLine($"    {workloadManifest.Version + '/' + workloadFeatureBand,align}");

                    reporter.Write($"{separator}{CliCommandStrings.WorkloadManifestPathColumn}:");
                    reporter.WriteLine($"       {workloadManifest.ManifestPath,align}");

                    reporter.Write($"{separator}{CliCommandStrings.WorkloadInstallTypeColumn}:");
                    reporter.WriteLine($"       {WorkloadInstallType.GetWorkloadInstallType(new SdkFeatureBand(Product.Version), dotnetPath).ToString(),align}"
                    );
                    reporter.WriteLine("");
                }
            }
        }

        if (!showVersion)
        {
            WriteUpdateModeAndAnyError();
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

    private static Command ConstructCommand()
    {
        DocumentedCommand command = new("workload", DocsLink, CliCommandStrings.WorkloadCommandDescription);

        command.Subcommands.Add(WorkloadInstallCommandParser.GetCommand());
        command.Subcommands.Add(WorkloadUpdateCommandParser.GetCommand());
        command.Subcommands.Add(WorkloadListCommandParser.GetCommand());
        command.Subcommands.Add(WorkloadSearchCommandParser.GetCommand());
        command.Subcommands.Add(WorkloadUninstallCommandParser.GetCommand());
        command.Subcommands.Add(WorkloadRepairCommandParser.GetCommand());
        command.Subcommands.Add(WorkloadRestoreCommandParser.GetCommand());
        command.Subcommands.Add(WorkloadCleanCommandParser.GetCommand());
        command.Subcommands.Add(WorkloadElevateCommandParser.GetCommand());
        command.Subcommands.Add(WorkloadConfigCommandParser.GetCommand());
        command.Subcommands.Add(WorkloadHistoryCommandParser.GetCommand());

        command.Options.Add(InfoOption);
        command.Options.Add(VersionOption);

        command.Validators.Add(commandResult =>
        {
            if (commandResult.HasOption(InfoOption) && commandResult.HasOption(VersionOption) && !commandResult.Children.Any(child => child is System.CommandLine.Parsing.CommandResult))
            {
                commandResult.AddError(CliStrings.RequiredCommandNotPassed);
            }
        });

        command.SetAction(ProcessArgs);

        return command;
    }
}
