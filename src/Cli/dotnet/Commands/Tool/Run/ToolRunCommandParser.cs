// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.Run;

internal static class ToolRunCommandParser
{
    public static readonly Argument<string> CommandNameArgument = new("commandName")
    {
        HelpName = CliCommandStrings.CommandNameArgumentName,
        Description = CliCommandStrings.CommandNameArgumentDescription
    };

    public static readonly Argument<IEnumerable<string>> CommandArgument = new("toolArguments")
    {
        Description = CliCommandStrings.ToolRunArgumentsDescription
    };

    public static readonly Option<bool> RollForwardOption = new("--allow-roll-forward")
    {
        Description = CliCommandStrings.RollForwardOptionDescription,
        Arity = ArgumentArity.Zero
    };

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("run", CliCommandStrings.ToolRunCommandDescription);

        command.Arguments.Add(CommandNameArgument);
        command.Arguments.Add(CommandArgument);
        command.Options.Add(RollForwardOption);

        command.SetAction((parseResult) => new ToolRunCommand(parseResult).Execute());

        return command;
    }
}
