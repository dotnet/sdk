// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Reference.Add;
using Microsoft.DotNet.Cli.Commands.Reference.List;
using Microsoft.DotNet.Cli.Commands.Reference.Remove;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Reference;

internal static class ReferenceCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-reference";

    public static readonly CliOption<string> ProjectOption = new CliOption<string>("--project")
    {
        Description = CliStrings.ProjectArgumentDescription,
        Recursive = true
    };

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
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
