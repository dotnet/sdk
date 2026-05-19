// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Reference.List;

namespace Microsoft.DotNet.Cli.Commands.Hidden.List.Reference;

internal static class ListReferenceCommandParser
{
    public static readonly Argument<string> Argument = new("argument") { Arity = ArgumentArity.ZeroOrOne, Hidden = true };

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var command = new Command("reference", CliCommandStrings.ReferenceListAppFullName);

        command.Arguments.Add(Argument);

        command.SetAction((parseResult) => new ReferenceListCommand(parseResult).Execute());

        return command;
    }
}
