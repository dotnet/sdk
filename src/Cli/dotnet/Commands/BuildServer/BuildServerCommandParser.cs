// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.BuildServer.Shutdown;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.BuildServer;

internal static class BuildServerCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-build-server";

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        var command = new DocumentedCommand("build-server", DocsLink, CliCommandStrings.BuildServerCommandDescription);

        command.Subcommands.Add(ServerShutdownCommandParser.GetCommand());

        command.SetAction((parseResult) => parseResult.HandleMissingCommand());

        return command;
    }
}
