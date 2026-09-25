// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
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
        command.ListCommand.SetAction((parseResult, cancellationToken) =>
            command.ListCommand.LocationOptions.IsGlobalOrToolPath(parseResult)
                ? throw new CommandNotAvailableInAotException()
                : new ToolListLocalCommand(parseResult).Execute(cancellationToken));
        command.UninstallCommand.SetAction((parseResult, cancellationToken) =>
            command.UninstallCommand.LocationOptions.IsGlobalOrToolPath(parseResult)
                ? throw new CommandNotAvailableInAotException()
                : new ToolUninstallLocalCommand(parseResult).Execute(cancellationToken));
        command.RunCommand.SetAction((parseResult, cancellationToken) => new ToolRunCommand(parseResult).Execute(cancellationToken));
        command.SearchCommand.SetAction((parseResult, cancellationToken) => new ToolSearchCommand(parseResult).Execute(cancellationToken));
#else
        command.SetAction(parseResult => parseResult.HandleMissingCommand());
        command.InstallCommand.SetAction((parseResult, cancellationToken) => new ToolInstallCommand(parseResult).Execute(cancellationToken));
        command.UninstallCommand.SetAction((parseResult, cancellationToken) => new ToolUninstallCommand(parseResult).Execute(cancellationToken));
        command.UpdateCommand.SetAction((parseResult, cancellationToken) => new ToolUpdateCommand(parseResult).Execute(cancellationToken));
        command.ListCommand.SetAction((parseResult, cancellationToken) => new ToolListCommand(parseResult).Execute(cancellationToken));
        command.RunCommand.SetAction((parseResult, cancellationToken) => new ToolRunCommand(parseResult).Execute(cancellationToken));
        command.SearchCommand.SetAction((parseResult, cancellationToken) => new ToolSearchCommand(parseResult).Execute(cancellationToken));
        command.RestoreCommand.SetAction((parseResult, cancellationToken) => new ToolRestoreCommand(parseResult).Execute(cancellationToken));
        command.ExecuteCommand.SetAction((parseResult, cancellationToken) => new ToolExecuteCommand(parseResult).Execute(cancellationToken));
#endif
    }
}
