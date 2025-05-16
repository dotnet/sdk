// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.Add.Package;
using Microsoft.DotNet.Cli.Commands.Hidden.Add.Reference;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Add;

internal static class AddCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-add";

    public static readonly Argument<string> ProjectArgument = new Argument<string>(CliStrings.ProjectArgumentName)
    {
        Description = CliStrings.ProjectArgumentDescription
    }.DefaultToCurrentDirectory();

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var command = new DocumentedCommand("add", DocsLink, CliCommandStrings.NetAddCommand)
        {
            Hidden = true
        };

        command.Arguments.Add(ProjectArgument);
        command.Subcommands.Add(AddPackageCommandParser.GetCommand());
        command.Subcommands.Add(AddReferenceCommandParser.GetCommand());

        command.SetAction((parseResult) => parseResult.HandleMissingCommand());

        return command;
    }
}
