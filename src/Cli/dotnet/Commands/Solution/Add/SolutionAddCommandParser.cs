// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Solution.Add;

public static class SolutionAddCommandParser
{
    public static readonly CliArgument<IEnumerable<string>> ProjectPathArgument = new(CliCommandStrings.AddProjectPathArgumentName)
    {
        HelpName = CliCommandStrings.AddProjectPathArgumentName,
        Description = CliCommandStrings.AddProjectPathArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore,
    };

    public static readonly CliOption<bool> InRootOption = new("--in-root")
    {
        Description = CliCommandStrings.InRoot
    };

    public static readonly CliOption<string> SolutionFolderOption = new("--solution-folder", "-s")
    {
        Description = CliCommandStrings.AddProjectSolutionFolderArgumentDescription
    };

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand command = new("add", CliCommandStrings.AddAppFullName);

        command.Arguments.Add(ProjectPathArgument);
        command.Options.Add(InRootOption);
        command.Options.Add(SolutionFolderOption);

        command.SetAction((parseResult) => new SolutionAddCommand(parseResult).Execute());

        return command;
    }
}
