// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Solution.List;

public sealed class SolutionListCommandDefinition : Command
{
    public readonly Option<bool> SolutionFolderOption = new("--solution-folders")
    {
        Description = CommandDefinitionStrings.ListSolutionFoldersArgumentDescription,
        Arity = ArgumentArity.Zero
    };

    public SolutionListCommandDefinition()
        : base("list", CommandDefinitionStrings.ListAppFullName)
    {
        Options.Add(SolutionFolderOption);
    }

    public SolutionCommandDefinition Parent => (SolutionCommandDefinition)Parents.Single();
}
