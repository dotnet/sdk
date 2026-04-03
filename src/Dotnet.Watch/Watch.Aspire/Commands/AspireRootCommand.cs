// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Watch;

internal sealed class AspireRootCommand : RootCommand
{
    public readonly AspireServerCommandDefinition ServerCommand = new();
    public readonly AspireResourceCommandDefinition ResourceCommand = new();
    public readonly AspireHostCommandDefinition HostCommand = new();

    public AspireRootCommand()
        : base("A supporting tool used by Aspire AppHost to launch and hot reload .NET applications. " +
               "Invoked automatically by the Aspire tooling; not intended for direct human use.")
    {
        Directives.Add(new EnvironmentVariablesDirective());

        Subcommands.Add(ServerCommand);
        Subcommands.Add(ResourceCommand);
        Subcommands.Add(HostCommand);
    }
}
