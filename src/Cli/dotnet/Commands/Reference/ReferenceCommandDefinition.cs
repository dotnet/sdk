// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Reference.Add;
using Microsoft.DotNet.Cli.Commands.Reference.List;
using Microsoft.DotNet.Cli.Commands.Reference.Remove;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Reference;

internal static class ReferenceCommandDefinition
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-reference";

    public static readonly Option<string> ProjectOption = new Option<string>("--project")
    {
        Description = CliStrings.ProjectArgumentDescription,
        Recursive = true
    };

    public static Command Create()
    {
        var command = new Command("reference", CliCommandStrings.NetRemoveCommand)
        {
            DocsLink = DocsLink
        };

        command.Subcommands.Add(ReferenceAddCommandParser.GetCommand());
        command.Subcommands.Add(ReferenceListCommandParser.GetCommand());
        command.Subcommands.Add(ReferenceRemoveCommandParser.GetCommand());
        command.Options.Add(ProjectOption);

        return command;
    }
}
