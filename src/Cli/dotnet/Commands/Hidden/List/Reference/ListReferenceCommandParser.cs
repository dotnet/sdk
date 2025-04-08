// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Reference.List;

namespace Microsoft.DotNet.Cli.Commands.Hidden.List.Reference;

internal static class ListReferenceCommandParser
{
    public static readonly CliArgument<string> Argument = new("argument") { Arity = ArgumentArity.ZeroOrOne, Hidden = true };

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        var command = new CliCommand("reference", CliCommandStrings.ReferenceListAppFullName);

        command.Arguments.Add(Argument);

        command.SetAction((parseResult) => new ReferenceListCommand(parseResult).Execute());

        return command;
    }
}
