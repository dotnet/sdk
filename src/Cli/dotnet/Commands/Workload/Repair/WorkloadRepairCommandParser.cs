// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Workload.Install;

namespace Microsoft.DotNet.Cli.Commands.Workload.Repair;

internal static class WorkloadRepairCommandParser
{
    public static readonly Option<string> ConfigOption = WorkloadRepairCommandDefinition.ConfigOption;

    public static readonly Option<string[]> SourceOption = WorkloadRepairCommandDefinition.SourceOption;

    public static readonly Option<string> VersionOption = WorkloadRepairCommandDefinition.VersionOption;

    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = WorkloadRepairCommandDefinition.VerbosityOption;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = WorkloadRepairCommandDefinition.Create();

        command.SetAction((parseResult) => new WorkloadRepairCommand(parseResult).Execute());

        return command;
    }
}
