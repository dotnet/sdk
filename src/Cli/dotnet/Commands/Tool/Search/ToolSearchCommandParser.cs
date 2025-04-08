// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.Search;

internal static class ToolSearchCommandParser
{
    public static readonly CliArgument<string> SearchTermArgument = new("searchTerm")
    {
        HelpName = CliCommandStrings.ToolSearchSearchTermArgumentName,
        Description = CliCommandStrings.ToolSearchSearchTermDescription
    };

    public static readonly CliOption<bool> DetailOption = new("--detail")
    {
        Description = CliCommandStrings.DetailDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly CliOption<string> SkipOption = new("--skip")
    {
        Description = CliCommandStrings.ToolSearchSkipDescription,
        HelpName = CliCommandStrings.ToolSearchSkipArgumentName
    };

    public static readonly CliOption<string> TakeOption = new("--take")
    {
        Description = CliCommandStrings.ToolSearchTakeDescription,
        HelpName = CliCommandStrings.ToolSearchTakeArgumentName
    };

    public static readonly CliOption<bool> PrereleaseOption = new("--prerelease")
    {
        Description = CliCommandStrings.ToolSearchPrereleaseDescription,
        Arity = ArgumentArity.Zero
    };

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand command = new("search", CliCommandStrings.ToolSearchCommandDescription);

        command.Arguments.Add(SearchTermArgument);

        command.Options.Add(DetailOption);
        command.Options.Add(SkipOption);
        command.Options.Add(TakeOption);
        command.Options.Add(PrereleaseOption);

        command.SetAction((parseResult) => new ToolSearchCommand(parseResult).Execute());

        return command;
    }
}
