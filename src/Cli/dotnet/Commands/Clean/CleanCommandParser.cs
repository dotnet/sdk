// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Clean.FileBasedAppArtifacts;

namespace Microsoft.DotNet.Cli.Commands.Clean;

internal static class CleanCommandParser
{
    private static readonly Command Command = SetAction(CleanCommandDefinition.Create());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command SetAction(Command command)
    {
        command.SetAction(CleanCommand.Run);

        command.Subcommands.Single(c => c.Name == CleanFileBasedAppArtifactsCommandDefinition.Name)
            .SetAction(parseResult => new CleanFileBasedAppArtifactsCommand(parseResult).Execute());

        return command;
    }
}
