// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Install;

internal static class WorkloadInstallCommandParser
{
    public static readonly Argument<IEnumerable<string>> WorkloadIdArgument = new("workloadId")
    {
        HelpName = CliCommandStrings.WorkloadIdArgumentName,
        Arity = ArgumentArity.OneOrMore,
        Description = CliCommandStrings.WorkloadIdArgumentDescription
    };

    public static readonly Option<bool> SkipSignCheckOption = new("--skip-sign-check")
    {
        Description = CliCommandStrings.SkipSignCheckOptionDescription,
        Hidden = true,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<bool> SkipManifestUpdateOption = new("--skip-manifest-update")
    {
        Description = CliCommandStrings.SkipManifestUpdateOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<string> TempDirOption = new("--temp-dir")
    {
        Description = CliCommandStrings.TempDirOptionDescription
    };

    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = CommonOptions.VerbosityOption(Utils.VerbosityOptions.normal);

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("install", CliCommandStrings.WorkloadInstallCommandDescription);

        command.Arguments.Add(WorkloadIdArgument);
        AddWorkloadInstallCommandOptions(command);

        command.SetAction((parseResult) => new WorkloadInstallCommand(parseResult).Execute());

        return command;
    }

    internal static void AddWorkloadInstallCommandOptions(Command command)
    {
        InstallingWorkloadCommandParser.AddWorkloadInstallCommandOptions(command);

        command.Options.Add(SkipManifestUpdateOption);
        command.Options.Add(TempDirOption);
        command.AddWorkloadCommandNuGetRestoreActionConfigOptions();
        command.Options.Add(VerbosityOption);
        command.Options.Add(SkipSignCheckOption);
        command.Options.Add(InstallingWorkloadCommandParser.WorkloadSetVersionOption);
    }
}
