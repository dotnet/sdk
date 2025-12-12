// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Solution.Add;

public static class SolutionAddCommandDefinition
{
    public const string Name = "add";

    public static readonly Argument<IEnumerable<string>> ProjectPathArgument = new(CliDefinitionResources.AddProjectPathArgumentName)
    {
        HelpName = CliDefinitionResources.AddProjectPathArgumentName,
        Description = CliDefinitionResources.AddProjectPathArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore,
    };

    public static readonly Option<bool> InRootOption = new("--in-root")
    {
        Description = CliDefinitionResources.InRoot
    };

    public static readonly Option<string> SolutionFolderOption = new("--solution-folder", "-s")
    {
        Description = CliDefinitionResources.AddProjectSolutionFolderArgumentDescription
    };

    public static readonly Option<bool> IncludeReferencesOption = new("--include-references")
    {
        Description = CliDefinitionResources.SolutionAddReferencedProjectsOptionDescription,
        DefaultValueFactory = (_) => true,
    };

    public static Command Create()
    {
        Command command = new(Name, CliDefinitionResources.AddAppFullName);

        command.Arguments.Add(ProjectPathArgument);
        command.Options.Add(InRootOption);
        command.Options.Add(SolutionFolderOption);
        command.Options.Add(IncludeReferencesOption);

        return command;
    }
}
