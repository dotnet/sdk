// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Solution.Add;
using Microsoft.DotNet.Cli.Commands.Solution.List;
using Microsoft.DotNet.Cli.Commands.Solution.Migrate;
using Microsoft.DotNet.Cli.Commands.Solution.Remove;

namespace Microsoft.DotNet.Cli.Commands.Solution;

public sealed class SolutionCommandDefinition : Command
{
    private const string Link = "https://aka.ms/dotnet-sln";

    public readonly Argument<string> SlnArgument = new Argument<string>(CommandDefinitionStrings.SolutionArgumentName)
    {
        HelpName = CommandDefinitionStrings.SolutionArgumentName,
        Description = CommandDefinitionStrings.SolutionArgumentDescription,
        Arity = ArgumentArity.ZeroOrOne
    }.DefaultToCurrentDirectory();

    public readonly SolutionAddCommandDefinition AddCommand = new();
    public readonly SolutionListCommandDefinition ListCommand = new();
    public readonly SolutionRemoveCommandDefinition RemoveCommand = new();
    public readonly SolutionMigrateCommandDefinition MigrateCommand = new();

    public SolutionCommandDefinition()
        : base("solution", CommandDefinitionStrings.SolutionAppFullName)
    {
        this.DocsLink = Link;

        Aliases.Add("sln");

        Arguments.Add(SlnArgument);
        Subcommands.Add(AddCommand);
        Subcommands.Add(ListCommand);
        Subcommands.Add(RemoveCommand);
        Subcommands.Add(MigrateCommand);
    }
}
