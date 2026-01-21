// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Execute;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Commands.Tool.List;
using Microsoft.DotNet.Cli.Commands.Tool.Restore;
using Microsoft.DotNet.Cli.Commands.Tool.Run;
using Microsoft.DotNet.Cli.Commands.Tool.Search;
using Microsoft.DotNet.Cli.Commands.Tool.Uninstall;
using Microsoft.DotNet.Cli.Commands.Tool.Update;

namespace Microsoft.DotNet.Cli.Commands.Tool;

internal sealed class ToolCommandDefinition : Command
{
    private const string Link = "https://aka.ms/dotnet-tool";

    public readonly ToolInstallCommandDefinition InstallCommand = new();
    public readonly ToolUninstallCommandDefinition UninstallCommand = new();
    public readonly ToolUpdateCommandDefinition UpdateCommand = new();
    public readonly ToolListCommandDefinition ListCommand = new();
    public readonly ToolRunCommandDefinition RunCommand = new();
    public readonly ToolSearchCommandDefinition SearchCommand = new();
    public readonly ToolRestoreCommandDefinition RestoreCommand = new();
    public readonly ToolExecuteCommandDefinition ExecuteCommand = new();

    public ToolCommandDefinition()
        : base("tool", CommandDefinitionStrings.ToolCommandDescription)
    {
        this.DocsLink = Link;

        Subcommands.Add(InstallCommand);
        Subcommands.Add(UninstallCommand);
        Subcommands.Add(UpdateCommand);
        Subcommands.Add(ListCommand);
        Subcommands.Add(RunCommand);
        Subcommands.Add(SearchCommand);
        Subcommands.Add(RestoreCommand);
        Subcommands.Add(ExecuteCommand);
    }
}
