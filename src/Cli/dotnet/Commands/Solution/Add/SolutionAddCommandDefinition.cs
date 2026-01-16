// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Solution.Add;

public sealed class SolutionAddCommandDefinition : Command
{
    public readonly Argument<IEnumerable<string>> ProjectPathArgument = new(CliCommandStrings.AddProjectPathArgumentName)
    {
        HelpName = CliCommandStrings.AddProjectPathArgumentName,
        Description = CliCommandStrings.AddProjectPathArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore,
    };

    public const string InRootOptionName = "--in-root";

    public readonly Option<bool> InRootOption = new(InRootOptionName)
    {
        Description = CliCommandStrings.InRoot
    };

    public const string SolutionFolderOptionName = "--solution-folder";

    public readonly Option<string> SolutionFolderOption = new(SolutionFolderOptionName, "-s")
    {
        Description = CliCommandStrings.AddProjectSolutionFolderArgumentDescription
    };

    public readonly Option<bool> IncludeReferencesOption = new("--include-references")
    {
        Description = CliCommandStrings.SolutionAddReferencedProjectsOptionDescription,
        DefaultValueFactory = (_) => true,
    };

    public SolutionAddCommandDefinition()
        : base("add", CliCommandStrings.AddAppFullName)
    {
        Arguments.Add(ProjectPathArgument);
        Options.Add(InRootOption);
        Options.Add(SolutionFolderOption);
        Options.Add(IncludeReferencesOption);
    }

    public SolutionCommandDefinition Parent => (SolutionCommandDefinition)Parents.Single();
}
