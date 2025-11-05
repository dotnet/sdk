// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.Dsc;

internal static class ToolDscManifestCommandParser
{
    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("manifest", CliCommandStrings.ToolDscManifestCommandDescription);

        command.SetAction((parseResult) => new ToolDscManifestCommand(parseResult).Execute());

        return command;
    }
}
