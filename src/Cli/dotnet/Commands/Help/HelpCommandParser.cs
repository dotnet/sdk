﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Help;

internal static class HelpCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-help";

    public static readonly CliArgument<string[]> Argument = new(CliCommandStrings.CommandArgumentName)
    {
        Description = CliCommandStrings.CommandArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        DocumentedCommand command = new("help", DocsLink, CliCommandStrings.HelpAppFullName);

        command.Arguments.Add(Argument);

        command.SetAction(HelpCommand.Run);

        return command;
    }
}

