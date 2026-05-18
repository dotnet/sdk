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
    public static void ConfigureCommand(ToolCommandDefinition command)
    {
        command.SetAction(parseResult => parseResult.HandleMissingCommand());

        command.InstallCommand.SetAction(parseResult => new ToolInstallCommand(parseResult).Execute());
        command.UninstallCommand.SetAction(parseResult => new ToolUninstallCommand(parseResult).Execute());
        command.UpdateCommand.SetAction(parseResult => new ToolUpdateCommand(parseResult).Execute());
        command.ListCommand.SetAction(parseResult => new ToolListCommand(parseResult).Execute());
        command.RunCommand.SetAction(parseResult => new ToolRunCommand(parseResult).Execute());
        command.SearchCommand.SetAction(parseResult => new ToolSearchCommand(parseResult).Execute());
        command.RestoreCommand.SetAction(parseResult => new ToolRestoreCommand(parseResult).Execute());
        command.ExecuteCommand.SetAction(parseResult => new ToolExecuteCommand(parseResult).Execute());
    }
}
