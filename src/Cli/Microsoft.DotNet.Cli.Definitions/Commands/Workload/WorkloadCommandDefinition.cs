// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Workload.Clean;
using Microsoft.DotNet.Cli.Commands.Workload.Config;
using Microsoft.DotNet.Cli.Commands.Workload.Elevate;
using Microsoft.DotNet.Cli.Commands.Workload.History;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Commands.Workload.List;
using Microsoft.DotNet.Cli.Commands.Workload.Repair;
using Microsoft.DotNet.Cli.Commands.Workload.Restore;
using Microsoft.DotNet.Cli.Commands.Workload.Search;
using Microsoft.DotNet.Cli.Commands.Workload.Uninstall;
using Microsoft.DotNet.Cli.Commands.Workload.Update;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli.Commands.Workload;

internal sealed class WorkloadCommandDefinition : Command
{
    private const string Link = "https://aka.ms/dotnet-workload";
    public new const string Name = "workload";

    public Option<bool> InfoOption = new("--info")
    {
        Description = CommandDefinitionStrings.WorkloadInfoDescription,
        Arity = ArgumentArity.Zero
    };

    public readonly Option<bool> VersionOption = new("--version")
    {
        Description = CommandDefinitionStrings.WorkloadVersionDescription,
        Arity = ArgumentArity.Zero
    };

    public readonly WorkloadInstallCommandDefinition InstallCommand = new();
    public readonly WorkloadUpdateCommandDefinition UpdateCommand = new();
    public readonly WorkloadListCommandDefinition ListCommand = new();
    public readonly WorkloadSearchCommandDefinition SearchCommand = new();
    public readonly WorkloadUninstallCommandDefinition UninstallCommand = new();
    public readonly WorkloadRepairCommandDefinition RepairCommand = new();
    public readonly WorkloadRestoreCommandDefinition RestoreCommand = new();
    public readonly WorkloadCleanCommandDefinition CleanCommand = new();
    public readonly WorkloadElevateCommandDefinition ElevateCommand = new();
    public readonly WorkloadConfigCommandDefinition ConfigCommand = new();
    public readonly WorkloadHistoryCommandDefinition HistoryCommand = new();

    public WorkloadCommandDefinition()
        : base(Name, CommandDefinitionStrings.WorkloadCommandDescription)
    {
        this.DocsLink = Link;

        Subcommands.Add(InstallCommand);
        Subcommands.Add(UpdateCommand);
        Subcommands.Add(ListCommand);
        Subcommands.Add(SearchCommand);
        Subcommands.Add(UninstallCommand);
        Subcommands.Add(RepairCommand);
        Subcommands.Add(RestoreCommand);
        Subcommands.Add(CleanCommand);
        Subcommands.Add(ElevateCommand);
        Subcommands.Add(ConfigCommand);
        Subcommands.Add(HistoryCommand);

        Options.Add(InfoOption);
        Options.Add(VersionOption);

        Validators.Add(commandResult =>
        {
            if (commandResult.HasOption(InfoOption) && commandResult.HasOption(VersionOption) && !commandResult.Children.Any(child => child is System.CommandLine.Parsing.CommandResult))
            {
                commandResult.AddError(CommandDefinitionStrings.RequiredCommandNotPassed);
            }
        });
    }
}
