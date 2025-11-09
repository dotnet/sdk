// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.VSTest;

internal static class VSTestCommandParser
{
    private static readonly Command Command = ConfigureCommand(VSTestCommandDefinition.Create());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConfigureCommand(Command command)
    {
        command.SetAction(VSTestCommand.Run);
        return command;
    }
}
