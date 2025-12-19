// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Workload.Install;

namespace Microsoft.DotNet.Cli.Commands.Workload.Update;

internal static class WorkloadUpdateCommandParser
{
    public static readonly Option<string> TempDirOption = WorkloadInstallCommandParser.TempDirOption;

    public static readonly Option<bool> FromPreviousSdkOption = new("--from-previous-sdk")
    {
        Description = CliCommandStrings.FromPreviousSdkOptionDescription
    };

    public static readonly Option<bool> AdManifestOnlyOption = new("--advertising-manifests-only")
    {
        Description = CliCommandStrings.AdManifestOnlyOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<bool> PrintRollbackOption = new("--print-rollback")
    {
        Hidden = true,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<int> FromHistoryOption = new("--from-history")
    {
        Description = CliCommandStrings.FromHistoryOptionDescription
    };

    public static readonly Option<string> HistoryManifestOnlyOption = new("--manifests-only")
    {
        Description = CliCommandStrings.HistoryManifestOnlyOptionDescription
    };

    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = CommonOptions.VerbosityOption(Utils.VerbosityOptions.normal);

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("update", CliCommandStrings.WorkloadUpdateCommandDescription);

        InstallingWorkloadCommandParser.AddWorkloadInstallCommandOptions(command);

        command.Options.Add(TempDirOption);
        command.Options.Add(FromPreviousSdkOption);
        command.Options.Add(AdManifestOnlyOption);
        command.Options.Add(InstallingWorkloadCommandParser.WorkloadSetVersionOption);
        command.AddWorkloadCommandNuGetRestoreActionConfigOptions();
        command.Options.Add(VerbosityOption);
        command.Options.Add(PrintRollbackOption);
        command.Options.Add(WorkloadInstallCommandParser.SkipSignCheckOption);
        command.Options.Add(FromHistoryOption);
        command.Options.Add(HistoryManifestOnlyOption);

        command.SetAction((parseResult) => new WorkloadUpdateCommand(parseResult).Execute());

        return command;
    }
}
