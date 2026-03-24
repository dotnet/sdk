// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Parse;

internal static class ParseCommandParser
{
    private static readonly ParseCommandDefinition Command = SetAction();

    public static Command GetCommand()
    {
        return Command;
    }

    private static ParseCommandDefinition SetAction()
    {
        var command = new ParseCommandDefinition();
        command.SetAction(ParseCommand.Run);
        return command;
    }
}
