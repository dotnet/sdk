// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.Runx;

internal static class ToolRunxCommandParser
{
    public static readonly CliArgument<string> CommandNameArgument = new("commandName")
    {
        HelpName = CliCommandStrings.CommandNameArgumentName,
        Description = CliCommandStrings.CommandNameArgumentDescription
    };

    public static readonly CliArgument<IEnumerable<string>> CommandArgument = new("toolArguments")
    {
        Description = "arguments forwarded to the tool"
    };

    public static readonly CliOption<bool> RollForwardOption = new("--allow-roll-forward")
    {
        Description = CliCommandStrings.RollForwardOptionDescription,
        Arity = ArgumentArity.Zero
    };

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand command = new("runx", "TODO: Run and execute remote tool");

        command.Arguments.Add(CommandNameArgument);
        command.Arguments.Add(CommandArgument);
        command.Options.Add(RollForwardOption);

        command.SetAction((parseResult) => new ToolRunxCommand(parseResult).Execute());

        return command;
    }
}
