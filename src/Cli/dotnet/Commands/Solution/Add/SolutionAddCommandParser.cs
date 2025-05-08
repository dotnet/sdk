// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Solution.Add;

public static class SolutionAddCommandParser
{
    public static readonly Argument<IEnumerable<string>> ProjectPathArgument = new(CliCommandStrings.AddProjectPathArgumentName)
    {
        HelpName = CliCommandStrings.AddProjectPathArgumentName,
        Description = CliCommandStrings.AddProjectPathArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore,
    };

    public static readonly Option<bool> InRootOption = new("--in-root")
    {
        Description = CliCommandStrings.InRoot
    };

    public static readonly Option<string> SolutionFolderOption = new("--solution-folder", "-s")
    {
        Description = CliCommandStrings.AddProjectSolutionFolderArgumentDescription
    };

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("add", CliCommandStrings.AddAppFullName);

        command.Arguments.Add(ProjectPathArgument);
        command.Options.Add(InRootOption);
        command.Options.Add(SolutionFolderOption);

        command.SetAction((parseResult) => new SolutionAddCommand(parseResult).Execute());

        return command;
    }
}
