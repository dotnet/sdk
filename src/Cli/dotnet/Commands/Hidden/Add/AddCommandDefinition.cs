// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.Add.Package;
using Microsoft.DotNet.Cli.Commands.Hidden.Add.Reference;
using Microsoft.DotNet.Cli.Commands.Package;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Add;

internal static class AddCommandDefinition
{
    public const string Name = "add";

    public static readonly string DocsLink = "https://aka.ms/dotnet-add";

    public static Command Create()
    {
        var command = new Command(Name, CliCommandStrings.NetAddCommand)
        {
            Hidden = true,
            DocsLink = DocsLink
        };

        command.Arguments.Add(PackageCommandDefinition.ProjectOrFileArgument);
        command.Subcommands.Add(AddPackageCommandDefinition.Create());
        command.Subcommands.Add(AddReferenceCommandDefinition.Create());

        return command;
    }
}
