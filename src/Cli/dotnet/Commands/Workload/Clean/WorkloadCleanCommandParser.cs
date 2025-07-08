// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Clean;

internal static class WorkloadCleanCommandParser
{
    public static readonly Option<bool> CleanAllOption = new("--all") { Description = CliCommandStrings.CleanAllOptionDescription };

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("clean", CliCommandStrings.WorkloadCleanCommandDescription);

        command.Options.Add(CleanAllOption);

        command.SetAction((parseResult) => new WorkloadCleanCommand(parseResult).Execute());

        return command;
    }
}
