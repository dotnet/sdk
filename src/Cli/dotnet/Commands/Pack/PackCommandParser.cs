// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Pack;

internal static class PackCommandParser
{
    private static readonly Command Command = SetAction(PackCommandDefinition.Create());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command SetAction(Command command)
    {
        command.SetAction(PackCommand.Run);
        return command;
    }
}
