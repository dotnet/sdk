// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Workload.Install;

namespace Microsoft.DotNet.Cli.Commands.Workload.Uninstall;

internal static class WorkloadUninstallCommandParser
{
    public static readonly Argument<IEnumerable<string>> WorkloadIdArgument = WorkloadInstallCommandParser.WorkloadIdArgument;
    public static readonly Option<string> VersionOption = InstallingWorkloadCommandParser.VersionOption;
    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = CommonOptions.VerbosityOption(Utils.VerbosityOptions.normal);

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("uninstall", CliCommandStrings.WorkloadUninstallCommandDescription);
        command.Arguments.Add(WorkloadIdArgument);
        command.Options.Add(WorkloadInstallCommandParser.SkipSignCheckOption);
        command.Options.Add(VerbosityOption);

        command.SetAction((parseResult) => new WorkloadUninstallCommand(parseResult).Execute());

        return command;
    }
}
