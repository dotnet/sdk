// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.StaticCompletions;
using Microsoft.DotNet.Cli.Commands.Build;
using Microsoft.DotNet.Cli.Commands.BuildServer;
using Microsoft.DotNet.Cli.Commands.Clean;
using Microsoft.DotNet.Cli.Commands.Dnx;
using Microsoft.DotNet.Cli.Commands.Format;
using Microsoft.DotNet.Cli.Commands.Fsi;
using Microsoft.DotNet.Cli.Commands.Help;
using Microsoft.DotNet.Cli.Commands.Hidden.Add;
using Microsoft.DotNet.Cli.Commands.Hidden.Complete;
using Microsoft.DotNet.Cli.Commands.Hidden.InternalReportInstallSuccess;
using Microsoft.DotNet.Cli.Commands.Hidden.List;
using Microsoft.DotNet.Cli.Commands.Hidden.Parse;
using Microsoft.DotNet.Cli.Commands.Hidden.Remove;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.New;
using Microsoft.DotNet.Cli.Commands.NuGet;
using Microsoft.DotNet.Cli.Commands.Pack;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Commands.Project;
using Microsoft.DotNet.Cli.Commands.Publish;
using Microsoft.DotNet.Cli.Commands.Reference;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Run.Api;
using Microsoft.DotNet.Cli.Commands.Sdk;
using Microsoft.DotNet.Cli.Commands.Solution;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Tool;
using Microsoft.DotNet.Cli.Commands.Tool.Store;
using Microsoft.DotNet.Cli.Commands.VSTest;
using Microsoft.DotNet.Cli.Commands.Workload;

namespace Microsoft.DotNet.Cli.Commands;

internal sealed class DotNetCommandDefinition : RootCommand
{
    public readonly Argument<string> DotnetSubCommand = new("subcommand")
    {
        Arity = ArgumentArity.ZeroOrOne,
        Hidden = true
    };

    public readonly Option<bool> DiagOption = CommonOptions.CreateDiagnosticsOption(recursive: false);

    public readonly Option<bool> VersionOption = new("--version")
    {
        Arity = ArgumentArity.Zero
    };

    public readonly Option<bool> InfoOption = new("--info")
    {
        Arity = ArgumentArity.Zero
    };

    public readonly Option<bool> ListSdksOption = new("--list-sdks")
    {
        Arity = ArgumentArity.Zero
    };

    public readonly Option<bool> ListRuntimesOption = new("--list-runtimes")
    {
        Arity = ArgumentArity.Zero
    };

    public readonly Option<bool> CliSchemaOption = new("--cli-schema")
    {
        Description = CommandDefinitionStrings.SDKSchemaCommandDefinition,
        Arity = ArgumentArity.Zero,
        Recursive = true,
        Hidden = true,
    };

    public readonly AddCommandDefinition AddCommand;
    public readonly BuildCommandDefinition BuildCommand;
    public readonly BuildServerCommandDefinition BuildServerCommand;
    public readonly CleanCommandDefinition CleanCommand;
    public readonly DnxCommandDefinition DnxCommand;
    public readonly FormatCommandDefinition FormatCommand;
    public readonly CompleteCommandDefinition CompleteCommand;
    public readonly FsiCommandDefinition FsiCommand;
    public readonly ListCommandDefinition ListCommand;
    public readonly MSBuildCommandDefinition MSBuildCommand;
    public readonly NewCommandDefinition NewCommand;
    public readonly NuGetCommandDefinition NuGetCommand;
    public readonly PackCommandDefinition PackCommand;
    public readonly PackageCommandDefinition PackageCommand;
    public readonly ParseCommandDefinition ParseCommand;
    public readonly ProjectCommandDefinition ProjectCommand;
    public readonly PublishCommandDefinition PublishCommand;
    public readonly ReferenceCommandDefinition ReferenceCommand;
    public readonly RemoveCommandDefinition RemoveCommand;
    public readonly RestoreCommandDefinition RestoreCommand;
    public readonly RunCommandDefinition RunCommand;
    public readonly RunApiCommandDefinition RunApiCommand;
    public readonly SolutionCommandDefinition SolutionCommand;
    public readonly StoreCommandDefinition StoreCommand;
    public readonly ToolCommandDefinition ToolCommand;
    public readonly VSTestCommandDefinition VSTestCommand;
    public readonly HelpCommandDefinition HelpCommand;
    public readonly SdkCommandDefinition SdkCommand;
    public readonly InternalReportInstallSuccessCommandDefinition InternalReportInstallSuccessCommand;
    public readonly WorkloadCommandDefinition WorkloadCommand;
    public readonly TestCommandDefinition TestCommand;
    public readonly CompletionsCommandDefinition CompletionsCommand;

    public DotNetCommandDefinition()
        : base("dotnet")
    {
        Directives.Add(new DiagramDirective());
        Directives.Add(new SuggestDirective());
        Directives.Add(new EnvironmentVariablesDirective());

        Arguments.Add(DotnetSubCommand);

        Options.Add(DiagOption);
        Options.Add(VersionOption);
        Options.Add(InfoOption);
        Options.Add(ListSdksOption);
        Options.Add(ListRuntimesOption);
        Options.Add(CliSchemaOption);

        Subcommands.Add(AddCommand = new());
        Subcommands.Add(BuildCommand = new());
        Subcommands.Add(BuildServerCommand = new());
        Subcommands.Add(CleanCommand = new());
        Subcommands.Add(DnxCommand = new());
        Subcommands.Add(FormatCommand = new());
        Subcommands.Add(CompleteCommand = new());
        Subcommands.Add(FsiCommand = new());
        Subcommands.Add(ListCommand = new());
        Subcommands.Add(MSBuildCommand = new());
        Subcommands.Add(NewCommand = new());
        Subcommands.Add(NuGetCommand = new());
        Subcommands.Add(PackCommand = new());
        Subcommands.Add(PackageCommand = new());
        Subcommands.Add(ParseCommand = new());
        Subcommands.Add(ProjectCommand = new());
        Subcommands.Add(PublishCommand = new());
        Subcommands.Add(ReferenceCommand = new());
        Subcommands.Add(RemoveCommand = new());
        Subcommands.Add(RestoreCommand = new());
        Subcommands.Add(RunCommand = new());
        Subcommands.Add(RunApiCommand = new());
        Subcommands.Add(SolutionCommand = new());
        Subcommands.Add(StoreCommand = new());
        Subcommands.Add(TestCommand = TestCommandDefinition.Create());
        Subcommands.Add(ToolCommand = new());
        Subcommands.Add(VSTestCommand = new());
        Subcommands.Add(HelpCommand = new());
        Subcommands.Add(SdkCommand = new());
        Subcommands.Add(InternalReportInstallSuccessCommand = new());
        Subcommands.Add(WorkloadCommand = new());
        Subcommands.Add(CompletionsCommand = new());
    }
}
