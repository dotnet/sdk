// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.BuildServer.Shutdown;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.BuildServer;

internal static class BuildServerCommandParser
{
    public static void ConfigureCommand(BuildServerCommandDefinition command)
    {
        command.SetAction(parseResult => parseResult.HandleMissingCommand());
        command.ShutdownCommand.SetAction(parseResult => new BuildServerShutdownCommand(parseResult).Execute());
    }
}
