// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Solution.Remove;

public static class SlnRemoveParser
{
    public static readonly CliArgument<IEnumerable<string>> ProjectPathArgument = new(CliCommandStrings.RemoveProjectPathArgumentName)
    {
        HelpName = CliCommandStrings.RemoveProjectPathArgumentName,
        Description = CliCommandStrings.RemoveProjectPathArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand command = new("remove", CliCommandStrings.RemoveAppFullName);

        command.Arguments.Add(ProjectPathArgument);

        command.SetAction((parseResult) => new RemoveProjectFromSolutionCommand(parseResult).Execute());

        return command;
    }
}
