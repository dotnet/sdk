// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Build;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Publish;

internal static class PublishCommandDefinition
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-publish";

    public static readonly Argument<string[]> SlnOrProjectOrFileArgument = new(CliStrings.SolutionOrProjectOrFileArgumentName)
    {
        Description = CliStrings.SolutionOrProjectOrFileArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public static readonly ImplicitRestoreOptions ImplicitRestoreOptions = new(showHelp: false, useShortOptions: false);

    public static readonly Option<string> OutputOption = new Option<string>("--output", "-o")
    {
        Description = CliCommandStrings.PublishOutputOptionDescription,
        HelpName = CliCommandStrings.PublishOutputOption
    }.ForwardAsOutputPath("PublishDir");

    public static readonly Option<IEnumerable<string>> ManifestOption = new Option<IEnumerable<string>>("--manifest")
    {
        Description = CliCommandStrings.ManifestOptionDescription,
        HelpName = CliCommandStrings.ManifestOption
    }.ForwardAsSingle(o => $"-property:TargetManifestFiles={string.Join("%3B", o.Select(CommandDirectoryContext.GetFullPath))}")
    .AllowSingleArgPerToken();

    public static readonly Option<bool> NoBuildOption = new Option<bool>("--no-build")
    {
        Description = CliCommandStrings.NoBuildOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:NoBuild=true");

    public static readonly Option<bool> NoLogoOption = CommonOptions.CreateNoLogoOption();

    public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

    public static readonly Option<bool> SelfContainedOption = CommonOptions.SelfContainedOption;

    public static readonly Option<bool> NoSelfContainedOption = CommonOptions.NoSelfContainedOption;

    public static readonly Option<string> RuntimeOption = CommonOptions.CreateRuntimeOption(CliCommandStrings.PublishRuntimeOptionDescription);

    public static readonly Option<string> FrameworkOption = CommonOptions.CreateFrameworkOption(CliCommandStrings.PublishFrameworkOptionDescription);

    public static readonly Option<string?> ConfigurationOption = CommonOptions.CreateConfigurationOption(CliCommandStrings.PublishConfigurationOptionDescription);
    public static readonly Option<string[]> TargetOption = CommonOptions.CreateRequiredMSBuildTargetOption("Publish", [("_IsPublishing", "true")]);

    public static readonly Option<Utils.VerbosityOptions?> VerbosityOption = BuildCommandDefinition.VerbosityOption;

    public static readonly Option<bool> NoDependenciesOption = RestoreCommandDefinition.CreateNoDependenciesOption(showHelp: false);
    public static readonly Option<string> ArtifactsPathOption = CommonOptions.CreateArtifactsPathOption();
    public static readonly Option<string> VersionSuffixOption = CommonOptions.CreateVersionSuffixOption();
    public static readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveMsBuildForwardOption();
    public static readonly Option<string> ArchitectureOption = CommonOptions.ArchitectureOption;
    public static readonly Option<string> OperatingSystemOption = CommonOptions.OperatingSystemOption;
    public static readonly Option<bool> DisableBuildServersOption = CommonOptions.CreateDisableBuildServersOption();
    public static readonly Option<string[]?> GetPropertyOption = CommonOptions.CreateGetPropertyOption();
    public static readonly Option<string[]?> GetItemOption = CommonOptions.CreateGetItemOption();
    public static readonly Option<string[]?> GetTargetResultOption = CommonOptions.CreateGetTargetResultOption();
    public static readonly Option<string[]?> GetResultOutputFileOption = CommonOptions.CreateGetResultOutputFileOption();

    public static Command Create()
    {
        var command = new Command("publish", CliCommandStrings.PublishAppDescription)
        {
            DocsLink = DocsLink
        };

        command.Arguments.Add(SlnOrProjectOrFileArgument);
        ImplicitRestoreOptions.AddTo(command.Options);
        command.Options.Add(NoDependenciesOption);
        command.Options.Add(OutputOption);
        command.Options.Add(ArtifactsPathOption);
        command.Options.Add(ManifestOption);
        command.Options.Add(NoBuildOption);
        command.Options.Add(SelfContainedOption);
        command.Options.Add(NoSelfContainedOption);
        command.Options.Add(NoLogoOption);
        command.Options.Add(FrameworkOption);
        command.Options.Add(RuntimeOption);
        command.Options.Add(ConfigurationOption);
        command.Options.Add(VersionSuffixOption);
        command.Options.Add(InteractiveOption);
        command.Options.Add(NoRestoreOption);
        command.Options.Add(VerbosityOption);
        command.Options.Add(ArchitectureOption);
        command.Options.Add(OperatingSystemOption);
        command.Options.Add(DisableBuildServersOption);
        command.Options.Add(TargetOption);
        command.Options.Add(GetPropertyOption);
        command.Options.Add(GetItemOption);
        command.Options.Add(GetTargetResultOption);
        command.Options.Add(GetResultOutputFileOption);

        return command;
    }
}
