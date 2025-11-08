// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.Add.Package;
using Microsoft.DotNet.Cli.Commands.Hidden.Add.Reference;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Add;

internal static class AddCommandDefinition
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-add";

    public static Command Create()
    {
        var command = new Command("add", CliCommandStrings.NetAddCommand)
        {
            Hidden = true,
            DocsLink = DocsLink
        };

        command.Arguments.Add(PackageCommandParser.ProjectOrFileArgument);
        command.Subcommands.Add(AddPackageCommandParser.GetCommand());
        command.Subcommands.Add(AddReferenceCommandParser.GetCommand());

        return command;
    }
}
