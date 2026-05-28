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
        // bare `dotnet tool` needs the full help output, which falls back to the managed CLI.
        command.SetAction((Func<ParseResult, int>)(_ => throw new CommandNotAvailableInAotException()));

        // Only the local `list`/`uninstall`, `run`, and `search` paths run in AOT. The
        // `--global`/`--tool-path` variants and `install`/`update`/`restore`/`execute` depend on
        // NuGet package install/restore infrastructure that isn't AOT-ready, so they throw
        // CommandNotAvailableInAotException; NativeEntryPoint catches it and hosts the managed CLI.
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

        command.InstallCommand.SetAction((Func<ParseResult, int>)(_ => throw new CommandNotAvailableInAotException()));
        command.UpdateCommand.SetAction((Func<ParseResult, int>)(_ => throw new CommandNotAvailableInAotException()));
        command.RestoreCommand.SetAction((Func<ParseResult, int>)(_ => throw new CommandNotAvailableInAotException()));
        command.ExecuteCommand.SetAction((Func<ParseResult, int>)(_ => throw new CommandNotAvailableInAotException()));
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
