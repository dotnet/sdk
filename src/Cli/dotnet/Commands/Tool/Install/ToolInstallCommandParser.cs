// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Commands.Tool.Common;
using Microsoft.DotNet.Cli.Commands.Tool.Search;
using Microsoft.DotNet.Cli.Extensions;
using NuGet.Packaging.Core;

namespace Microsoft.DotNet.Cli.Commands.Tool.Install;

internal static class ToolInstallCommandParser
{
    public static readonly CliArgument<PackageIdentity?> PackageIdentityArgument = CommonArguments.PackageIdentityArgument();

    public static readonly CliOption<string> VersionOption = new("--version")
    {
        Description = CliCommandStrings.ToolInstallVersionOptionDescription,
        HelpName = CliCommandStrings.ToolInstallVersionOptionName
    };

    public static readonly CliOption<string> ConfigOption = new("--configfile")
    {
        Description = CliCommandStrings.ToolInstallConfigFileOptionDescription,
        HelpName = CliCommandStrings.ToolInstallConfigFileOptionName
    };

    public static readonly CliOption<string[]> SourceOption = new CliOption<string[]>("--source")
    {
        Description = CliCommandStrings.ToolInstallSourceOptionDescription,
        HelpName = CliCommandStrings.ToolInstallSourceOptionName
    }.AllowSingleArgPerToken();

    public static readonly CliOption<string[]> AddSourceOption = new CliOption<string[]>("--add-source")
    {
        Description = CliCommandStrings.ToolInstallAddSourceOptionDescription,
        HelpName = CliCommandStrings.ToolInstallAddSourceOptionName
    }.AllowSingleArgPerToken();

    public static readonly CliOption<string> FrameworkOption = new("--framework")
    {
        Description = CliCommandStrings.ToolInstallFrameworkOptionDescription,
        HelpName = CliCommandStrings.ToolInstallFrameworkOptionName
    };

    public static readonly CliOption<bool> PrereleaseOption = ToolSearchCommandParser.PrereleaseOption;

    public static readonly CliOption<bool> CreateManifestIfNeededOption = new("--create-manifest-if-needed")
    {
        Description = CliCommandStrings.CreateManifestIfNeededOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly CliOption<bool> AllowPackageDowngradeOption = new("--allow-downgrade")
    {
        Description = CliCommandStrings.AllowPackageDowngradeOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly CliOption<VerbosityOptions> VerbosityOption = CommonOptions.VerbosityOption;

    // Don't use the common options version as we don't want this to be a forwarded option
    public static readonly CliOption<string> ArchitectureOption = new("--arch", "-a")
    {
        Description = CliStrings.ArchitectureOptionDescription
    };

    public static readonly CliOption<bool> RollForwardOption = new("--allow-roll-forward")
    {
        Description = CliCommandStrings.RollForwardOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly CliOption<bool> GlobalOption = ToolAppliedOption.GlobalOption;

    public static readonly CliOption<bool> LocalOption = ToolAppliedOption.LocalOption;

    public static readonly CliOption<string> ToolPathOption = ToolAppliedOption.ToolPathOption;

    public static readonly CliOption<string> ToolManifestOption = ToolAppliedOption.ToolManifestOption;

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand command = new("install", CliCommandStrings.ToolInstallCommandDescription);
        command.Arguments.Add(PackageIdentityArgument);

        AddCommandOptions(command);

        command.Options.Add(ArchitectureOption);
        command.Options.Add(CreateManifestIfNeededOption);
        command.Options.Add(AllowPackageDowngradeOption);
        command.Options.Add(RollForwardOption);

        command.SetAction((parseResult) => new ToolInstallCommand(parseResult).Execute());

        return command;
    }

    public static CliCommand AddCommandOptions(CliCommand command)
    {
        command.Options.Add(GlobalOption.WithHelpDescription(command, CliCommandStrings.ToolInstallGlobalOptionDescription));
        command.Options.Add(LocalOption.WithHelpDescription(command, CliCommandStrings.ToolInstallLocalOptionDescription));
        command.Options.Add(ToolPathOption.WithHelpDescription(command, CliCommandStrings.ToolInstallToolPathOptionDescription));
        command.Options.Add(VersionOption);
        command.Options.Add(ConfigOption);
        command.Options.Add(ToolManifestOption.WithHelpDescription(command, CliCommandStrings.ToolInstallManifestPathOptionDescription));
        command.Options.Add(AddSourceOption);
        command.Options.Add(SourceOption);
        command.Options.Add(FrameworkOption);
        command.Options.Add(PrereleaseOption);
        command.Options.Add(ToolCommandRestorePassThroughOptions.DisableParallelOption);
        command.Options.Add(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption);
        command.Options.Add(ToolCommandRestorePassThroughOptions.NoCacheOption);
        command.Options.Add(ToolCommandRestorePassThroughOptions.NoHttpCacheOption);
        command.Options.Add(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption);
        command.Options.Add(VerbosityOption);

        return command;
    }
}
