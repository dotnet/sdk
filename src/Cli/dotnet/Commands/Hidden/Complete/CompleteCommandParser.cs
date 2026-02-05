// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Complete;

internal static class CompleteCommandParser
{
    private static readonly CompleteCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static CompleteCommandDefinition CreateCommand()
    {
        var command = new CompleteCommandDefinition();
        command.SetAction(CompleteCommand.Run);
        return command;
    }
}
