// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Common;
using Microsoft.DotNet.Cli.Commands.Tool.Search;

namespace Microsoft.DotNet.Cli.Commands.Tool.Install;

internal static class ToolInstallCommandParser
{
    public static readonly Argument<PackageIdentityWithRange> PackageIdentityArgument = ToolInstallCommandDefinition.PackageIdentityArgument;

    public static readonly Option<string> VersionOption = ToolInstallCommandDefinition.VersionOption;

    public static readonly Option<string> ConfigOption = ToolInstallCommandDefinition.ConfigOption;

    public static readonly Option<string[]> SourceOption = ToolInstallCommandDefinition.SourceOption;

    public static readonly Option<string[]> AddSourceOption = ToolInstallCommandDefinition.AddSourceOption;

    public static readonly Option<string> FrameworkOption = ToolInstallCommandDefinition.FrameworkOption;

    public static readonly Option<bool> PrereleaseOption = ToolInstallCommandDefinition.PrereleaseOption;

    public static readonly Option<bool> CreateManifestIfNeededOption = ToolInstallCommandDefinition.CreateManifestIfNeededOption;

    public static readonly Option<bool> AllowPackageDowngradeOption = ToolInstallCommandDefinition.AllowPackageDowngradeOption;

    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = ToolInstallCommandDefinition.VerbosityOption;

    public static readonly Option<string> ArchitectureOption = ToolInstallCommandDefinition.ArchitectureOption;

    public static readonly Option<bool> RollForwardOption = ToolInstallCommandDefinition.RollForwardOption;

    public static readonly Option<bool> GlobalOption = ToolInstallCommandDefinition.GlobalOption;

    public static readonly Option<bool> LocalOption = ToolInstallCommandDefinition.LocalOption;

    public static readonly Option<string> ToolPathOption = ToolInstallCommandDefinition.ToolPathOption;

    public static readonly Option<string> ToolManifestOption = ToolInstallCommandDefinition.ToolManifestOption;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = ToolInstallCommandDefinition.Create();

        command.SetAction((parseResult) => new ToolInstallCommand(parseResult).Execute());

        return command;
    }

    public static Command AddCommandOptions(Command command)
    {
        return ToolInstallCommandDefinition.AddCommandOptions(command);
    }
}
