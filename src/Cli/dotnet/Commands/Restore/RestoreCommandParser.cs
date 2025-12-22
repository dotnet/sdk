// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Restore;

internal static class RestoreCommandParser
{
    private static readonly Command Command = SetAction(RestoreCommandDefinition.Create());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command SetAction(Command command)
    {
        command.SetAction(RestoreCommand.Run);
        return command;
    }
}
