// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Solution.Remove;

public sealed class SolutionRemoveCommandDefinition : Command
{
    public readonly Argument<IEnumerable<string>> ProjectPathArgument = new(CommandDefinitionStrings.RemoveProjectPathArgumentName)
    {
        HelpName = CommandDefinitionStrings.RemoveProjectPathArgumentName,
        Description = CommandDefinitionStrings.RemoveProjectPathArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public SolutionRemoveCommandDefinition()
        : base("remove", CommandDefinitionStrings.RemoveAppFullName)
    {
        Arguments.Add(ProjectPathArgument);
    }

    public SolutionCommandDefinition Parent => (SolutionCommandDefinition)Parents.Single();
}
