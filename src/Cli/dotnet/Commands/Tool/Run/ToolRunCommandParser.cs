// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.Run;

internal static class ToolRunCommandParser
{
    public static readonly Argument<string> CommandNameArgument = ToolRunCommandDefinition.CommandNameArgument;

    public static readonly Argument<IEnumerable<string>> CommandArgument = ToolRunCommandDefinition.CommandArgument;

    public static readonly Option<bool> RollForwardOption = ToolRunCommandDefinition.RollForwardOption;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = ToolRunCommandDefinition.Create();

        command.SetAction((parseResult) => new ToolRunCommand(parseResult).Execute());

        return command;
    }
}
