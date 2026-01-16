// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.BuildServer.Shutdown;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.BuildServer;

internal static class BuildServerCommandParser
{
    private static readonly BuildServerCommandDefinition Command = CreateCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static BuildServerCommandDefinition CreateCommand()
    {
        var command = new BuildServerCommandDefinition();
        command.SetAction(parseResult => parseResult.HandleMissingCommand());
        command.ShutdownCommand.SetAction(parseResult => new BuildServerShutdownCommand(parseResult).Execute());
        return command;
    }
}
