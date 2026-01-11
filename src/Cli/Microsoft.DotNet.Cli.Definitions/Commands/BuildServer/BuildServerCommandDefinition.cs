// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.BuildServer.Shutdown;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.BuildServer;

internal sealed class BuildServerCommandDefinition : Command
{
    private const string Link = "https://aka.ms/dotnet-build-server";

    public readonly BuildServerShutdownCommandDefinition ShutdownCommand = new();

    public BuildServerCommandDefinition()
        : base("build-server", CommandDefinitionStrings.BuildServerCommandDescription)
    {
        this.DocsLink = Link;
        Subcommands.Add(ShutdownCommand);
    }
}
