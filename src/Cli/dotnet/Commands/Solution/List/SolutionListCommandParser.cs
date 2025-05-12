// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Solution.List;

public static class SolutionListCommandParser
{
    public static readonly Option<bool> SolutionFolderOption = new("--solution-folders")
    {
        Description = CliCommandStrings.ListSolutionFoldersArgumentDescription,
        Arity = ArgumentArity.Zero
    };

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("list", CliCommandStrings.ListAppFullName);

        command.Options.Add(SolutionFolderOption);
        command.SetAction((parseResult) => new SolutionListCommand(parseResult).Execute());

        return command;
    }
}
