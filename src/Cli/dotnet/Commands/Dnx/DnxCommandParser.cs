// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Execute;

namespace Microsoft.DotNet.Cli.Commands.Dnx;

internal static class DnxCommandParser
{
    public static readonly DnxCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    public static DnxCommandDefinition CreateCommand()
    {
        var command = new DnxCommandDefinition();
        command.SetAction(parseResult => new ToolExecuteCommand(parseResult).Execute());
        return command;
    }
}
