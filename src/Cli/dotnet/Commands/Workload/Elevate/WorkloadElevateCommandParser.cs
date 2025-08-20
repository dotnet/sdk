// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Elevate;

internal static class WorkloadElevateCommandParser
{
    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("elevate", CliCommandStrings.WorkloadElevateCommandDescription)
        {
            Hidden = true
        };

        command.SetAction((parseResult) => new WorkloadElevateCommand(parseResult).Execute());

        return command;
    }
}
