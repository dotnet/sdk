// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.BuildServer;
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
using Microsoft.Extensions.EnvironmentAbstractions;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli.Commands.Workload;

internal static class WorkloadCommandParser
{
    public static void ConfigureCommand(WorkloadCommandDefinition def)
        => ConfigureCommand(
            def,
            (parseResult, cancellationToken) => new WorkloadUpdateCommand(parseResult).Execute(cancellationToken),
            new MSBuildServer());

    internal static void ConfigureCommand(
        WorkloadCommandDefinition def,
        Func<ParseResult, CancellationToken, int> executeUpdate,
        IBuildServer msbuildServer)
    {
        def.SetAction(parseResult => parseResult.HandleMissingCommand());
        def.InfoOption.Action = new ShowWorkloadsInfoAction();
        def.VersionOption.Action = new ShowWorkloadsVersionOption();

        def.InstallCommand.SetAction((parseResult, cancellationToken) => new WorkloadInstallCommand(parseResult).Execute(cancellationToken));
        def.UpdateCommand.SetAction((parseResult, cancellationToken) =>
        {
            bool shouldShutdown =
                !parseResult.GetValue(def.UpdateCommand.PrintDownloadLinkOnlyOption) &&
                string.IsNullOrWhiteSpace(parseResult.GetValue(def.UpdateCommand.DownloadToCacheOption)) &&
                !parseResult.GetValue(def.UpdateCommand.AdManifestOnlyOption) &&
                !parseResult.GetValue(def.UpdateCommand.PrintRollbackOption);

            try
            {
                return executeUpdate(parseResult, cancellationToken);
            }
            finally
            {
                if (shouldShutdown)
                {
                    try
                    {
                        msbuildServer.Shutdown(cancellationToken);
                    }
                    catch (Exception e)
                    {
                        Reporter.Verbose.WriteLine(e.ToString());
                    }
                }
            }
        });
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

    /// <summary>
    /// Builds the <see cref="PackageSourceLocation"/> described by the <c>--configfile</c> and <c>--source</c>
    /// options, or <see langword="null"/> if neither was specified.
    /// </summary>
    public static PackageSourceLocation? ToPackageSourceLocation(this ParseResult parseResult, Option<string> configOption, Option<string[]> sourceOption)
    {
        var configFile = parseResult.GetValue(configOption);
        var sources = parseResult.GetValue(sourceOption);

        return string.IsNullOrEmpty(configFile) && (sources is null || sources.Length == 0) ? null :
            new PackageSourceLocation(string.IsNullOrEmpty(configFile) ? null : new FilePath(configFile), sourceFeedOverrides: sources);
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
