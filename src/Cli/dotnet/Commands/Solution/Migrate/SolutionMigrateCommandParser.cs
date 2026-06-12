// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Solution.Migrate;

public static class SolutionMigrateCommandParser
{
    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("migrate", CliCommandStrings.MigrateAppFullName);

        command.Arguments.Add(SolutionCommandParser.SlnArgument);

        command.SetAction((parseResult) => new SolutionMigrateCommand(parseResult).Execute());

        return command;
    }
}
