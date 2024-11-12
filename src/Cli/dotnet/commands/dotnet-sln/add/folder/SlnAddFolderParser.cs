// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Sln.Add;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli;

public static class SlnAddFolderParser
{
    public static readonly CliArgument<IEnumerable<string>> FolderPathArgument = new(LocalizableStrings.AddFolderPathArgumentName)
    {
        HelpName = LocalizableStrings.AddFolderPathArgumentName,
        Description = LocalizableStrings.AddFolderPathArgumentDescription,
        Arity = ArgumentArity.OneOrMore,
    };

    public static readonly CliOption<bool> InRootOption = new("--in-root")
    {
        // TODO: "Place *project* in root of the solution" isn't right
        Description = LocalizableStrings.InRoot
    };

    public static readonly CliOption<string> SolutionFolderOption = new("--solution-folder", "-s")
    {
        Description = LocalizableStrings.AddFolderSolutionFolderArgumentDescription
    };

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand command = new("folder", LocalizableStrings.AddFolderFullName);

        // TODO: Any intermediate folders in the 'destination folder' should be created as necessary
        // TODO: If the 'destination folder' exists, the files should be added to it, a new folder should not be created
        command.Arguments.Add(FolderPathArgument);

        command.Options.Add(InRootOption);
        command.Options.Add(SolutionFolderOption);

        command.SetAction((parseResult) => new AddFolderToSolutionCommand(parseResult).Execute());

        return command;
    }
}
