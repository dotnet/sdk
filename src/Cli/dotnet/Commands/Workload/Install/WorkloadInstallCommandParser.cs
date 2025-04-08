// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install;

internal static class WorkloadInstallCommandParser
{
    public static readonly CliArgument<IEnumerable<string>> WorkloadIdArgument = new("workloadId")
    {
        HelpName = CliCommandStrings.WorkloadIdArgumentName,
        Arity = ArgumentArity.OneOrMore,
        Description = CliCommandStrings.WorkloadIdArgumentDescription
    };

    public static readonly CliOption<bool> SkipSignCheckOption = new("--skip-sign-check")
    {
        Description = CliCommandStrings.SkipSignCheckOptionDescription,
        Hidden = true,
        Arity = ArgumentArity.Zero
    };

    public static readonly CliOption<bool> SkipManifestUpdateOption = new("--skip-manifest-update")
    {
        Description = CliCommandStrings.SkipManifestUpdateOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly CliOption<string> TempDirOption = new("--temp-dir")
    {
        Description = CliCommandStrings.TempDirOptionDescription
    };

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand command = new("install", CliCommandStrings.WorkloadInstallCommandDescription);

        command.Arguments.Add(WorkloadIdArgument);
        AddWorkloadInstallCommandOptions(command);

        command.SetAction((parseResult) => new WorkloadInstallCommand(parseResult).Execute());

        return command;
    }

    internal static void AddWorkloadInstallCommandOptions(CliCommand command)
    {
        InstallingWorkloadCommandParser.AddWorkloadInstallCommandOptions(command);

        command.Options.Add(SkipManifestUpdateOption);
        command.Options.Add(TempDirOption);
        command.AddWorkloadCommandNuGetRestoreActionConfigOptions();
        command.Options.Add(CommonOptions.VerbosityOption);
        command.Options.Add(SkipSignCheckOption);
        command.Options.Add(InstallingWorkloadCommandParser.WorkloadSetVersionOption);
    }
}
