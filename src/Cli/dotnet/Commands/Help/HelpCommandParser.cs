// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Help;

internal static class HelpCommandParser
{
    private static readonly HelpCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static HelpCommandDefinition CreateCommand()
    {
        var command = new HelpCommandDefinition();
        command.SetAction(HelpCommand.Run);
        return command;
    }
}
