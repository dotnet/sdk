// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Execute;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Commands.Tool.List;
using Microsoft.DotNet.Cli.Commands.Tool.Restore;
using Microsoft.DotNet.Cli.Commands.Tool.Run;
using Microsoft.DotNet.Cli.Commands.Tool.Search;
using Microsoft.DotNet.Cli.Commands.Tool.Uninstall;
using Microsoft.DotNet.Cli.Commands.Tool.Update;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Tool;

internal static class ToolCommandParser
{
    private static readonly Command Command = ConfigureCommand(ToolCommandDefinition.Create());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConfigureCommand(Command command)
    {
        command.SetAction((parseResult) => parseResult.HandleMissingCommand());
        return command;
    }
}
