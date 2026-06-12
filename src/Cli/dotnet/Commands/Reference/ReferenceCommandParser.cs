// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Reference.Add;
using Microsoft.DotNet.Cli.Commands.Reference.List;
using Microsoft.DotNet.Cli.Commands.Reference.Remove;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Reference;

internal static class ReferenceCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-reference";

    public static readonly Option<string> ProjectOption = new Option<string>("--project")
    {
        Description = CliStrings.ProjectArgumentDescription,
        Recursive = true
    };

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var command = new DocumentedCommand("reference", DocsLink, CliCommandStrings.NetRemoveCommand);

        command.Subcommands.Add(ReferenceAddCommandParser.GetCommand());
        command.Subcommands.Add(ReferenceListCommandParser.GetCommand());
        command.Subcommands.Add(ReferenceRemoveCommandParser.GetCommand());
        command.Options.Add(ProjectOption);
        command.SetAction((parseResult) => parseResult.HandleMissingCommand());

        return command;
    }
}
