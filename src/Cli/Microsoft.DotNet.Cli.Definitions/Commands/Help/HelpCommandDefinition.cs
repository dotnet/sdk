// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Help;

internal sealed class HelpCommandDefinition : Command
{
    private const string Link = "https://aka.ms/dotnet-help";

    public readonly Argument<string[]> Argument = new(CommandDefinitionStrings.CommandArgumentName)
    {
        Description = CommandDefinitionStrings.CommandArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public HelpCommandDefinition()
        : base("help", CommandDefinitionStrings.HelpAppFullName)
    {
        this.DocsLink = Link;
        Arguments.Add(Argument);
    }
}

