// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Workload.Install;

namespace Microsoft.DotNet.Cli.Commands.Workload.Update;

internal static class WorkloadUpdateCommandParser
{
    public static readonly CliOption<string> TempDirOption = WorkloadInstallCommandParser.TempDirOption;

    public static readonly CliOption<bool> FromPreviousSdkOption = new("--from-previous-sdk")
    {
        Description = CliCommandStrings.FromPreviousSdkOptionDescription
    };

    public static readonly CliOption<bool> AdManifestOnlyOption = new("--advertising-manifests-only")
    {
        Description = CliCommandStrings.AdManifestOnlyOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly CliOption<bool> PrintRollbackOption = new("--print-rollback")
    {
        Hidden = true,
        Arity = ArgumentArity.Zero
    };

    public static readonly CliOption<int> FromHistoryOption = new("--from-history")
    {
        Description = CliCommandStrings.FromHistoryOptionDescription
    };

    public static readonly CliOption<string> HistoryManifestOnlyOption = new("--manifests-only")
    {
        Description = CliCommandStrings.HistoryManifestOnlyOptionDescription
    };

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand command = new("update", CliCommandStrings.WorkloadUpdateCommandDescription);

        InstallingWorkloadCommandParser.AddWorkloadInstallCommandOptions(command);

        command.Options.Add(TempDirOption);
        command.Options.Add(FromPreviousSdkOption);
        command.Options.Add(AdManifestOnlyOption);
        command.Options.Add(InstallingWorkloadCommandParser.WorkloadSetVersionOption);
        command.AddWorkloadCommandNuGetRestoreActionConfigOptions();
        command.Options.Add(CommonOptions.VerbosityOption);
        command.Options.Add(PrintRollbackOption);
        command.Options.Add(WorkloadInstallCommandParser.SkipSignCheckOption);
        command.Options.Add(FromHistoryOption);
        command.Options.Add(HistoryManifestOnlyOption);

        command.SetAction((parseResult) => new WorkloadUpdateCommand(parseResult).Execute());

        return command;
    }
}
