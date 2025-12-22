// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.BuildServer.Shutdown;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.BuildServer;

internal static class BuildServerCommandParser
{
    private static readonly Command Command = SetAction(BuildServerCommandDefinition.Create());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command SetAction(Command command)
    {
        command.SetAction((parseResult) => parseResult.HandleMissingCommand());
        command.Subcommands.Single(c => c.Name == BuildServerShutdownCommandDefinition.Name).SetAction((parseResult) => new BuildServerShutdownCommand(parseResult).Execute());
        return command;
    }
}
