// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Tool.Dsc;

internal static class ToolDscTestCommandParser
{
    public static readonly Option<string> InputOption = new("--input", "-i")
    {
        Description = CliCommandStrings.ToolDscInputOptionDescription,
        HelpName = CliCommandStrings.ToolDscInputOptionName
    };

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("test", CliCommandStrings.ToolDscTestCommandDescription);
        command.Options.Add(InputOption);

        command.SetAction((parseResult) => new ToolDscTestCommand(parseResult).Execute());

        return command;
    }
}
