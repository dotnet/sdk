// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Reference.List;

internal static class ReferenceListCommandParser
{
    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var command = new Command("list", CliCommandStrings.ReferenceListAppFullName);

        command.SetAction((parseResult) => new ReferenceListCommand(parseResult).Execute());

        return command;
    }
}
