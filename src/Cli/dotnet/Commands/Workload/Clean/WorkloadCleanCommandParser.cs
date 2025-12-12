// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Clean;

internal static class WorkloadCleanCommandParser
{
    public static readonly Option<bool> CleanAllOption = WorkloadCleanCommandDefinition.CleanAllOption;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = WorkloadCleanCommandDefinition.Create();

        command.SetAction((parseResult) => new WorkloadCleanCommand(parseResult).Execute());

        return command;
    }
}
