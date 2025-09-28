// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Workload.Install;

namespace Microsoft.DotNet.Cli.Commands.Workload.Repair;

internal static class WorkloadRepairCommandParser
{
    public static readonly Option<string> ConfigOption = InstallingWorkloadCommandParser.ConfigOption;

    public static readonly Option<string[]> SourceOption = InstallingWorkloadCommandParser.SourceOption;

    public static readonly Option<string> VersionOption = InstallingWorkloadCommandParser.VersionOption;

    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = CommonOptions.VerbosityOption(Utils.VerbosityOptions.normal);

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("repair", CliCommandStrings.WorkloadRepairCommandDescription);

        command.Options.Add(VersionOption);
        command.Options.Add(ConfigOption);
        command.Options.Add(SourceOption);
        command.Options.Add(VerbosityOption);
        command.AddWorkloadCommandNuGetRestoreActionConfigOptions();
        command.Options.Add(WorkloadInstallCommandParser.SkipSignCheckOption);

        command.SetAction((parseResult) => new WorkloadRepairCommand(parseResult).Execute());

        return command;
    }
}
