// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Solution.Add;
using Microsoft.DotNet.Cli.Commands.Solution.List;
using Microsoft.DotNet.Cli.Commands.Solution.Migrate;
using Microsoft.DotNet.Cli.Commands.Solution.Remove;

namespace Microsoft.DotNet.Cli.Commands.Solution;

internal static class SolutionCommandDefinition
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-sln";

    public static readonly string CommandName = "solution";
    public static readonly string CommandAlias = "sln";
    public static readonly Argument<string> SlnArgument = new Argument<string>(CliCommandStrings.SolutionArgumentName)
    {
        HelpName = CliCommandStrings.SolutionArgumentName,
        Description = CliCommandStrings.SolutionArgumentDescription,
        Arity = ArgumentArity.ZeroOrOne
    }.DefaultToCurrentDirectory();

    public static Command Create()
    {
        Command command = new(CommandName, CliCommandStrings.SolutionAppFullName)
        {
            DocsLink = DocsLink
        };

        command.Aliases.Add(CommandAlias);

        command.Arguments.Add(SlnArgument);
        command.Subcommands.Add(SolutionAddCommandDefinition.Create());
        command.Subcommands.Add(SolutionListCommandDefinition.Create());
        command.Subcommands.Add(SolutionRemoveCommandDefinition.Create());
        command.Subcommands.Add(SolutionMigrateCommandDefinition.Create());

        return command;
    }
}
