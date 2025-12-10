// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.BuildServer.Shutdown;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.BuildServer;

internal static class BuildServerCommandDefinition
{
    public const string Name = "build-server";
    public static readonly string DocsLink = "https://aka.ms/dotnet-build-server";

    public static Command Create()
    {
        var command = new Command(Name, CliCommandStrings.BuildServerCommandDescription)
        {
            DocsLink = DocsLink
        };

        command.Subcommands.Add(BuildServerShutdownCommandDefinition.Create());

        return command;
    }
}
