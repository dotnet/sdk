// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Solution.Remove;

public static class SolutionRemoveCommandParser
{
    public static readonly Argument<IEnumerable<string>> ProjectPathArgument = new(CliCommandStrings.RemoveProjectPathArgumentName)
    {
        HelpName = CliCommandStrings.RemoveProjectPathArgumentName,
        Description = CliCommandStrings.RemoveProjectPathArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("remove", CliCommandStrings.RemoveAppFullName);

        command.Arguments.Add(ProjectPathArgument);

        command.SetAction((parseResult) => new SolutionRemoveCommand(parseResult).Execute());

        return command;
    }
}
