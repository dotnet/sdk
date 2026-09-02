// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal static class EnvCommandParser
{
    private static readonly Command s_envCommand = ConstructCommand();

    public static Command GetCommand() => s_envCommand;

    private static Command ConstructCommand()
    {
        Command command = new("env", Strings.EnvCommandDescription);

        command.Subcommands.Add(EnvSetCommandParser.ConstructCommand());
        command.Subcommands.Add(EnvClearCommandParser.ConstructCommand());
        command.Subcommands.Add(EnvShowCommandParser.ConstructCommand());
        command.Subcommands.Add(EnvScriptCommandParser.ConstructCommand(name: "script"));

        return command;
    }
}

