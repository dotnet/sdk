// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.SelfSign;

internal sealed class SelfSignCommandDefinition : Command
{
    public new const string Name = "selfsign";

    public readonly Argument<string[]> PathsArgument = new(CommandDefinitionStrings.SelfSignPathsArgumentName)
    {
        Description = CommandDefinitionStrings.SelfSignPathsArgumentDescription,
        Arity = ArgumentArity.OneOrMore
    };

    public readonly Option<bool> ForceOption = new("--force")
    {
        Description = CommandDefinitionStrings.SelfSignForceOptionDescription
    };

    public SelfSignCommandDefinition()
        : base(Name, CommandDefinitionStrings.SelfSignAppFullName)
    {
        Arguments.Add(PathsArgument);
        Options.Add(ForceOption);
    }
}
