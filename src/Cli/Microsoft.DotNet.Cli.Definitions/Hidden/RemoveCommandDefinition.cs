// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.Remove.Package;
using Microsoft.DotNet.Cli.Commands.Hidden.Remove.Reference;
using Microsoft.DotNet.Cli.Commands.Package;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Remove;

internal static class RemoveCommandDefinition
{
    public const string Name = "remove";

    public static readonly string DocsLink = "https://aka.ms/dotnet-remove";

    public static Command Create()
    {
        var command = new Command(Name, CliCommandStrings.NetRemoveCommand)
        {
            Hidden = true,
            DocsLink = DocsLink
        };

        command.Arguments.Add(PackageCommandDefinition.ProjectOrFileArgument);
        command.Subcommands.Add(RemovePackageCommandDefinition.Create());
        command.Subcommands.Add(RemoveReferenceCommandDefinition.Create());

        return command;
    }
}
