// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Run;

internal static class RunCommandParser
{
    private static readonly RunCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static RunCommandDefinition CreateCommand()
    {
        var command = new RunCommandDefinition();
        command.SetAction(RunCommand.Run);
        return command;
    }
}
