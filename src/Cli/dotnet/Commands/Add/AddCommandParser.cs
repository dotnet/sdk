// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Add.Package;
using Microsoft.DotNet.Cli.Commands.Add.Reference;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Add;

internal static class AddCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-add";

    public static readonly CliArgument<string> ProjectArgument = new CliArgument<string>(CliStrings.ProjectArgumentName)
    {
        Description = CliStrings.ProjectArgumentDescription
    }.DefaultToCurrentDirectory();

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        var command = new DocumentedCommand("add", DocsLink, CliCommandStrings.NetAddCommand)
        {
            Hidden = true
        };

        command.Arguments.Add(ProjectArgument);
        command.Subcommands.Add(AddPackageParser.GetCommand());
        command.Subcommands.Add(AddProjectToProjectReferenceParser.GetCommand());

        command.SetAction((parseResult) => parseResult.HandleMissingCommand());

        return command;
    }
}
