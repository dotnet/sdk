// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Common;
using Microsoft.DotNet.Cli.Commands.Tool.Search;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Tool.Install;

internal static class ToolInstallCommandParser
{
    public static readonly Argument<PackageIdentityWithRange> PackageIdentityArgument = CommonArguments.RequiredPackageIdentityArgument("dotnetsay", "2.1.7");

    public static readonly Option<string> VersionOption = new("--version")
    {
        Description = CliCommandStrings.ToolInstallVersionOptionDescription,
        HelpName = CliCommandStrings.ToolInstallVersionOptionName
    };

    public static readonly Option<string> ConfigOption = new("--configfile")
    {
        Description = CliCommandStrings.ToolInstallConfigFileOptionDescription,
        HelpName = CliCommandStrings.ToolInstallConfigFileOptionName
    };

    public static readonly Option<string[]> SourceOption = new Option<string[]>("--source")
    {
        Description = CliCommandStrings.ToolInstallSourceOptionDescription,
        HelpName = CliCommandStrings.ToolInstallSourceOptionName
    }.AllowSingleArgPerToken();

    public static readonly Option<string[]> AddSourceOption = new Option<string[]>("--add-source")
    {
        Description = CliCommandStrings.ToolInstallAddSourceOptionDescription,
        HelpName = CliCommandStrings.ToolInstallAddSourceOptionName
    }.AllowSingleArgPerToken();

    public static readonly Option<string> FrameworkOption = new("--framework")
    {
        Description = CliCommandStrings.ToolInstallFrameworkOptionDescription,
        HelpName = CliCommandStrings.ToolInstallFrameworkOptionName
    };

    public static readonly Option<bool> PrereleaseOption = ToolSearchCommandParser.PrereleaseOption;

    public static readonly Option<bool> CreateManifestIfNeededOption = new("--create-manifest-if-needed")
    {
        Description = CliCommandStrings.CreateManifestIfNeededOptionDescription,
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => true,
    };

    public static readonly Option<bool> AllowPackageDowngradeOption = new("--allow-downgrade")
    {
        Description = CliCommandStrings.AllowPackageDowngradeOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = CommonOptions.VerbosityOption(Utils.VerbosityOptions.normal);

    // Don't use the common options version as we don't want this to be a forwarded option
    public static readonly Option<string> ArchitectureOption = new("--arch", "-a")
    {
        Description = CliStrings.ArchitectureOptionDescription
    };

    public static readonly Option<bool> RollForwardOption = new("--allow-roll-forward")
    {
        Description = CliCommandStrings.RollForwardOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<bool> GlobalOption = ToolAppliedOption.GlobalOption(CliCommandStrings.ToolInstallGlobalOptionDescription);

    public static readonly Option<bool> LocalOption = ToolAppliedOption.LocalOption(CliCommandStrings.ToolInstallLocalOptionDescription);

    public static readonly Option<string> ToolPathOption = ToolAppliedOption.ToolPathOption(CliCommandStrings.ToolInstallToolPathOptionDescription);

    public static readonly Option<string> ToolManifestOption = ToolAppliedOption.ToolManifestOption(CliCommandStrings.ToolInstallManifestPathOptionDescription);

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("install", CliCommandStrings.ToolInstallCommandDescription);
        command.Arguments.Add(PackageIdentityArgument);

        AddCommandOptions(command);

        command.Options.Add(ArchitectureOption);
        command.Options.Add(CreateManifestIfNeededOption);
        command.Options.Add(AllowPackageDowngradeOption);
        command.Options.Add(RollForwardOption);

        command.SetAction((parseResult) => new ToolInstallCommand(parseResult).Execute());

        return command;
    }

    public static Command AddCommandOptions(Command command)
    {
        command.Options.Add(GlobalOption);
        command.Options.Add(LocalOption);
        command.Options.Add(ToolPathOption);
        command.Options.Add(VersionOption);
        command.Options.Add(ConfigOption);
        command.Options.Add(ToolManifestOption);
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
