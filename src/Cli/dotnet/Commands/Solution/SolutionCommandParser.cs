// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Solution.Add;
using Microsoft.DotNet.Cli.Commands.Solution.List;
using Microsoft.DotNet.Cli.Commands.Solution.Migrate;
using Microsoft.DotNet.Cli.Commands.Solution.Remove;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Solution;

internal static class SolutionCommandParser
{
    private static readonly Command Command = ConfigureCommand(SolutionCommandDefinition.Create());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConfigureCommand(Command command)
    {
        command.SetAction(parseResult => parseResult.HandleMissingCommand());

        command.Subcommands.Single(c => c.Name == SolutionAddCommandDefinition.Name).SetAction(parseResult => new SolutionAddCommand(parseResult).Execute());
        command.Subcommands.Single(c => c.Name == SolutionListCommandDefinition.Name).SetAction(parseResult => new SolutionListCommand(parseResult).Execute());
        command.Subcommands.Single(c => c.Name == SolutionMigrateCommandDefinition.Name).SetAction(parseResult => new SolutionMigrateCommand(parseResult).Execute());
        command.Subcommands.Single(c => c.Name == SolutionRemoveCommandDefinition.Name).SetAction(parseResult => new SolutionRemoveCommand(parseResult).Execute());

        return command;
    }
}
