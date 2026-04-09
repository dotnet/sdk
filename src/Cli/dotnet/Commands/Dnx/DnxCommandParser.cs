// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool;
using Microsoft.DotNet.Cli.Commands.Tool.Execute;

namespace Microsoft.DotNet.Cli.Commands.Dnx;

internal static class DnxCommandParser
{
    public static readonly Command Command = ConstructCommand();
    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("dnx", CliCommandStrings.ToolExecuteCommandDescription);
        command.Hidden = true;

        foreach (var argument in ToolExecuteCommandParser.Command.Arguments)
        {
            command.Arguments.Add(argument);
        }

        foreach (var option in ToolExecuteCommandParser.Command.Options)
        {
            command.Options.Add(option);
        }

        command.SetAction((parseResult) => new ToolExecuteCommand(parseResult).Execute());

        return command;
    }
}
