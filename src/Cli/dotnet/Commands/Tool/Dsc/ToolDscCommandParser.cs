// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Tool.Dsc;

internal static class ToolDscCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-tool-dsc";

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        DocumentedCommand command = new("dsc", DocsLink, CliCommandStrings.ToolDscCommandDescription);

        command.Subcommands.Add(ToolDscGetCommandParser.GetCommand());
        command.Subcommands.Add(ToolDscSetCommandParser.GetCommand());
        command.Subcommands.Add(ToolDscTestCommandParser.GetCommand());
        command.Subcommands.Add(ToolDscExportCommandParser.GetCommand());
        command.Subcommands.Add(ToolDscSchemaCommandParser.GetCommand());
        command.Subcommands.Add(ToolDscManifestCommandParser.GetCommand());

        command.SetAction((parseResult) => parseResult.HandleMissingCommand());

        return command;
    }
}
