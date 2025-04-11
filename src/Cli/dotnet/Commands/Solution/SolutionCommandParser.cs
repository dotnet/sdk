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
    public static readonly string DocsLink = "https://aka.ms/dotnet-sln";

    public static readonly string CommandName = "solution";
    public static readonly string CommandAlias = "sln";
    public static readonly CliArgument<string> SlnArgument = new CliArgument<string>(CliCommandStrings.SolutionArgumentName)
    {
        HelpName = CliCommandStrings.SolutionArgumentName,
        Description = CliCommandStrings.SolutionArgumentDescription,
        Arity = ArgumentArity.ZeroOrOne
    }.DefaultToCurrentDirectory();

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        DocumentedCommand command = new(CommandName, DocsLink, CliCommandStrings.SolutionAppFullName);

        command.Aliases.Add(CommandAlias);

        command.Arguments.Add(SlnArgument);
        command.Subcommands.Add(SolutionAddCommandParser.GetCommand());
        command.Subcommands.Add(SolutionListCommandParser.GetCommand());
        command.Subcommands.Add(SolutionRemoveCommandParser.GetCommand());
        command.Subcommands.Add(SolutionMigrateCommandParser.GetCommand());

        command.SetAction((parseResult) => parseResult.HandleMissingCommand());

        return command;
    }
}
