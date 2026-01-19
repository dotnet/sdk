// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Pack;

internal static class PackCommandParser
{
    private static readonly PackCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static PackCommandDefinition CreateCommand()
    {
        var command = new PackCommandDefinition();
        command.SetAction(PackCommand.Run);
        return command;
    }
}
