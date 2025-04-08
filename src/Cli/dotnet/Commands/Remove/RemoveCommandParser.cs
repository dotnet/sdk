// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Remove.Package;
using Microsoft.DotNet.Cli.Commands.Remove.Reference;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Remove;

internal static class RemoveCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-remove";

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
        var command = new DocumentedCommand("remove", DocsLink, CliCommandStrings.NetRemoveCommand)
        {
            Hidden = true
        };

        command.Arguments.Add(ProjectArgument);
        command.Subcommands.Add(RemovePackageParser.GetCommand());
        command.Subcommands.Add(RemoveProjectToProjectReferenceParser.GetCommand());

        command.SetAction((parseResult) => parseResult.HandleMissingCommand());

        return command;
    }
}
