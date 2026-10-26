// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.History;

internal static class WorkloadHistoryCommandParser
{
    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var command = new Command("history", CliCommandStrings.WorkloadHistoryCommandDescription);

        command.SetAction(parseResult => new WorkloadHistoryCommand(parseResult).Execute());

        return command;
    }
}
