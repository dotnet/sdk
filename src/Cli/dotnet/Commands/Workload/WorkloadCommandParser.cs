// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.DotNet.Cli.CommandLine;
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

        def.InstallCommand.SetAction((parseResult, cancellationToken) => new WorkloadInstallCommand(parseResult).Execute(cancellationToken));
        def.UpdateCommand.SetAction((parseResult, cancellationToken) => new WorkloadUpdateCommand(parseResult).Execute(cancellationToken));
        def.ListCommand.SetAction((parseResult, cancellationToken) => new WorkloadListCommand(parseResult).Execute(cancellationToken));
        def.SearchCommand.SetAction((parseResult, cancellationToken) => new WorkloadSearchCommand(parseResult).Execute(cancellationToken));
        def.SearchCommand.VersionCommand.SetAction((parseResult, cancellationToken) => new WorkloadSearchVersionsCommand(parseResult).Execute(cancellationToken));
        def.UninstallCommand.SetAction((parseResult, cancellationToken) => new WorkloadUninstallCommand(parseResult).Execute(cancellationToken));
        def.RepairCommand.SetAction((parseResult, cancellationToken) => new WorkloadRepairCommand(parseResult).Execute(cancellationToken));
        def.RestoreCommand.SetAction((parseResult, cancellationToken) => new WorkloadRestoreCommand(parseResult).Execute(cancellationToken));
        def.CleanCommand.SetAction((parseResult, cancellationToken) => new WorkloadCleanCommand(parseResult).Execute(cancellationToken));
        def.ElevateCommand.SetAction((parseResult, cancellationToken) => new WorkloadElevateCommand(parseResult).Execute(cancellationToken));
        def.ConfigCommand.SetAction((parseResult, cancellationToken) => new WorkloadConfigCommand(parseResult).Execute(cancellationToken));
        def.HistoryCommand.SetAction((parseResult, cancellationToken) => new WorkloadHistoryCommand(parseResult).Execute(cancellationToken));
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
