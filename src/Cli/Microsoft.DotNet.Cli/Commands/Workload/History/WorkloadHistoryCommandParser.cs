// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Workloads.Workload.History;

namespace Microsoft.DotNet.Cli;

internal static class WorkloadHistoryCommandParser
{
    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        var command = new CliCommand("history", LocalizableStrings.CommandDescription);

        command.SetAction(parseResult => new WorkloadHistoryCommand(parseResult).Execute());

        return command;
    }
}
