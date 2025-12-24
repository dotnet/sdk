// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
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
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli.Commands.Workload;

internal static class WorkloadCommandParser
{
    private static readonly WorkloadCommandDefinition Command = ConfigureCommand(new());

    public static Command GetCommand()
    {
        return Command;
    }

    public static WorkloadCommandDefinition ConfigureCommand(WorkloadCommandDefinition def)
    {
        def.SetAction(parseResult => parseResult.HandleMissingCommand());
        def.InfoOption.Action = new ShowWorkloadsInfoAction();
        def.VersionOption.Action = new ShowWorkloadsVersionOption();

        WorkloadInstallCommandParser.ConfigureCommand(def.InstallCommand);
        WorkloadUpdateCommandParser.ConfigureCommand(def.UpdateCommand);
        WorkloadListCommandParser.ConfigureCommand(def.ListCommand);
        WorkloadSearchCommandParser.ConfigureCommand(def.SearchCommand);
        WorkloadUninstallCommandParser.ConfigureCommand(def.UninstallCommand);
        WorkloadRepairCommandParser.ConfigureCommand(def.RepairCommand);
        WorkloadRestoreCommandParser.ConfigureCommand(def.RestoreCommand);
        WorkloadCleanCommandParser.ConfigureCommand(def.CleanCommand);
        WorkloadElevateCommandParser.ConfigureCommand(def.ElevateCommand);
        WorkloadConfigCommandParser.ConfigureCommand(def.ConfigCommand);
        WorkloadHistoryCommandParser.ConfigureCommand(def.HistoryCommand);

        return def;
    }

    public static RestoreActionConfig ToRestoreActionConfig(this WorkloadCommandNuGetRestoreActionConfigOptions options, ParseResult parseResult)
    {
        return new RestoreActionConfig(DisableParallel: parseResult.GetValue(options.DisableParallelOption),
            NoCache: parseResult.GetValue(options.NoCacheOption) || parseResult.GetValue(options.NoHttpCacheOption),
            IgnoreFailedSources: parseResult.GetValue(options.IgnoreFailedSourcesOption),
            Interactive: parseResult.GetValue(options.InteractiveOption));
    }

    private sealed class ShowWorkloadsInfoAction : SynchronousCommandLineAction
    {
        public override bool Terminating => true;

        public override int Invoke(ParseResult parseResult)
        {
            new WorkloadInfoHelper(isInteractive: false).ShowWorkloadsInfo();
            Reporter.Output.WriteLine(string.Empty);
            return 0;
        }
    }

    private sealed class ShowWorkloadsVersionOption : SynchronousCommandLineAction
    {
        public override bool Terminating => true;

        public override int Invoke(ParseResult parseResult)
        {
            Reporter.Output.WriteLine(WorkloadInfoHelper.GetWorkloadsVersion());
            Reporter.Output.WriteLine(string.Empty);
            return 0;
        }
    }
}
