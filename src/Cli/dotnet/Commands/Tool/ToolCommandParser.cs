// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
#if !CLI_AOT
using Microsoft.DotNet.Cli.Commands.Tool.Execute;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Commands.Tool.Restore;
using Microsoft.DotNet.Cli.Commands.Tool.Update;
using Microsoft.DotNet.Cli.Extensions;
#endif
using Microsoft.DotNet.Cli.Commands.Tool.List;
using Microsoft.DotNet.Cli.Commands.Tool.Run;
using Microsoft.DotNet.Cli.Commands.Tool.Search;
using Microsoft.DotNet.Cli.Commands.Tool.Uninstall;

namespace Microsoft.DotNet.Cli.Commands.Tool;

internal static class ToolCommandParser
{
    public static void ConfigureCommand(ToolCommandDefinition command)
    {
#if CLI_AOT
        // ConfigureAotActions already set every `tool` subcommand (and the bare `tool` command) to
        // throw CommandNotAvailableInAotException by default, so we only override the paths that run
        // in AOT. Only the local `list`/`uninstall`, `run`, and `search` paths are AOT-capable; the
        // `--global`/`--tool-path` variants and `install`/`update`/`restore`/`execute` keep the
        // default fallback because they depend on NuGet package install/restore infrastructure that
        // isn't AOT-ready. NativeEntryPoint catches the exception and hosts the managed CLI.
        command.ListCommand.SetAction(parseResult =>
            command.ListCommand.LocationOptions.IsGlobalOrToolPath(parseResult)
                ? throw new CommandNotAvailableInAotException()
                : new ToolListLocalCommand(parseResult).Execute());
        command.UninstallCommand.SetAction(parseResult =>
            command.UninstallCommand.LocationOptions.IsGlobalOrToolPath(parseResult)
                ? throw new CommandNotAvailableInAotException()
                : new ToolUninstallLocalCommand(parseResult).Execute());
        command.RunCommand.SetAction(parseResult => new ToolRunCommand(parseResult).Execute());
        command.SearchCommand.SetAction(parseResult => new ToolSearchCommand(parseResult).Execute());
#else
        command.SetAction(parseResult => parseResult.HandleMissingCommand());
        command.InstallCommand.SetAction(parseResult => new ToolInstallCommand(parseResult).Execute());
        command.UninstallCommand.SetAction(parseResult => new ToolUninstallCommand(parseResult).Execute());
        command.UpdateCommand.SetAction(parseResult => new ToolUpdateCommand(parseResult).Execute());
        command.ListCommand.SetAction(parseResult => new ToolListCommand(parseResult).Execute());
        command.RunCommand.SetAction(parseResult => new ToolRunCommand(parseResult).Execute());
        command.SearchCommand.SetAction(parseResult => new ToolSearchCommand(parseResult).Execute());
        command.RestoreCommand.SetAction(parseResult => new ToolRestoreCommand(parseResult).Execute());
        command.ExecuteCommand.SetAction(parseResult => new ToolExecuteCommand(parseResult).Execute());
#endif
    }
}
