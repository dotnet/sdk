// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Clean.FileBasedAppArtifacts;

namespace Microsoft.DotNet.Cli.Commands.Clean;

internal static class CleanCommandParser
{
    private static readonly CleanCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static CleanCommandDefinition CreateCommand()
    {
        var command = new CleanCommandDefinition();
        command.SetAction(CleanCommand.Run);
        command.FileBasedAppsCommand.SetAction(parseResult => new CleanFileBasedAppArtifactsCommand(parseResult).Execute());
        return command;
    }
}
