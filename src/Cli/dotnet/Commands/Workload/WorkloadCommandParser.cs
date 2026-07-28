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
    public static void ConfigureCommand(WorkloadCommandDefinition def)
    {
        def.SetAction(parseResult => parseResult.HandleMissingCommand());
        def.InfoOption.Action = new ShowWorkloadsInfoAction();
        def.VersionOption.Action = new ShowWorkloadsVersionOption();

        def.InstallCommand.SetAction(parseResult => new WorkloadInstallCommand(parseResult).Execute());
        def.UpdateCommand.SetAction(parseResult => new WorkloadUpdateCommand(parseResult).Execute());
        def.ListCommand.SetAction(parseResult => new WorkloadListCommand(parseResult).Execute());
        def.SearchCommand.SetAction(parseResult => new WorkloadSearchCommand(parseResult).Execute());
        def.SearchCommand.VersionCommand.SetAction(parseResult => new WorkloadSearchVersionsCommand(parseResult).Execute());
        def.UninstallCommand.SetAction(parseResult => new WorkloadUninstallCommand(parseResult).Execute());
        def.RepairCommand.SetAction(parseResult => new WorkloadRepairCommand(parseResult).Execute());
        def.RestoreCommand.SetAction(parseResult => new WorkloadRestoreCommand(parseResult).Execute());
        def.CleanCommand.SetAction(parseResult => new WorkloadCleanCommand(parseResult).Execute());
        def.ElevateCommand.SetAction(parseResult => new WorkloadElevateCommand(parseResult).Execute());
        def.ConfigCommand.SetAction(parseResult => new WorkloadConfigCommand(parseResult).Execute());
        def.HistoryCommand.SetAction(parseResult => new WorkloadHistoryCommand(parseResult).Execute());
    }

    public static RestoreActionConfig ToRestoreActionConfig(this NuGetRestoreOptions options, ParseResult parseResult)
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
