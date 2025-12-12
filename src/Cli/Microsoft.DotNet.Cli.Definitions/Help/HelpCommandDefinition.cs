// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Help;

internal static class HelpCommandDefinition
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-help";

    public static readonly Argument<string[]> Argument = new(CliDefinitionResources.CommandArgumentName)
    {
        Description = CliDefinitionResources.CommandArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public static Command Create()
    {
        Command command = new("help", CliDefinitionResources.HelpAppFullName)
        {
            DocsLink = DocsLink
        };

        command.Arguments.Add(Argument);

        return command;
    }
}

