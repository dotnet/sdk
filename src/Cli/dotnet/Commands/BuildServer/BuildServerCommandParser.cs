// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.BuildServer.Shutdown;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.BuildServer;

internal static partial class BuildServerCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-build-server";

    public static Command CreateCommandDefinition()
    {
        var command = new Command("build-server", CliCommandStrings.BuildServerCommandDescription)
        {
            DocsLink = DocsLink
        };

        command.Subcommands.Add(BuildServerShutdownCommandParser.GetCommand());

        return command;
    }
}
