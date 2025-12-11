// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.List;

internal static class WorkloadListCommandParser
{
    // arguments are a list of workload to be detected
    public static readonly Option<bool> MachineReadableOption = WorkloadListCommandDefinition.MachineReadableOption;

    public static readonly Option<string> VersionOption = WorkloadListCommandDefinition.VersionOption;

    public static readonly Option<string> TempDirOption = WorkloadListCommandDefinition.TempDirOption;

    public static readonly Option<bool> IncludePreviewsOption = WorkloadListCommandDefinition.IncludePreviewsOption;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = WorkloadListCommandDefinition.Create();

        command.SetAction((parseResult) => new WorkloadListCommand(parseResult).Execute());

        return command;
    }
}
