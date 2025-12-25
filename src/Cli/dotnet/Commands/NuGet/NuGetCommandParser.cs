// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.NuGet;

internal static class NuGetCommandParser
{
    private static readonly Command Command = SetAction(NuGetCommandDefinition.Create());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command SetAction(Command command)
    {
        command.SetAction(NuGetCommand.Run);

        foreach (var subcommand in command.Subcommands)
        {
            subcommand.SetAction(NuGetCommand.Run);
        }

        return command;
    }
}

