// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Restore;

namespace Microsoft.DotNet.Cli.Commands.Publish;

internal sealed class PublishCommandDefinition : Command
{
    private const string Link = "https://aka.ms/dotnet-publish";

    public readonly Argument<string[]> SlnOrProjectOrFileArgument = new(CommandDefinitionStrings.SolutionOrProjectOrFileArgumentName)
    {
        Description = CommandDefinitionStrings.SolutionOrProjectOrFileArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public readonly ImplicitRestoreOptions ImplicitRestoreOptions = new(showHelp: false, useShortOptions: false);

    public readonly Option<string> OutputOption = new Option<string>("--output", "-o")
    {
        Description = CommandDefinitionStrings.PublishOutputOptionDescription,
        HelpName = CommandDefinitionStrings.PublishOutputOption
    }.ForwardAsOutputPath("PublishDir");

    public readonly Option<IEnumerable<string>> ManifestOption = new Option<IEnumerable<string>>("--manifest")
    {
        Description = CommandDefinitionStrings.ManifestOptionDescription,
        HelpName = CommandDefinitionStrings.ManifestOption
    }.ForwardAsSingle(o => $"-property:TargetManifestFiles={string.Join("%3B", o.Select(CommandDirectoryContext.GetFullPath))}")
    .AllowSingleArgPerToken();

    public readonly Option<bool> NoBuildOption = new Option<bool>("--no-build")
    {
        Description = CommandDefinitionStrings.NoBuildOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:NoBuild=true");

    public readonly Option<bool> NoLogoOption = CommonOptions.CreateNoLogoOption();

    public readonly Option<bool> NoRestoreOption = CommonOptions.CreateNoRestoreOption();

    public readonly Option<bool> SelfContainedOption = CommonOptions.CreateSelfContainedOption();

    public readonly Option<bool> NoSelfContainedOption = CommonOptions.CreateNoSelfContainedOption();

    public readonly TargetPlatformOptions TargetPlatformOptions = new(CommandDefinitionStrings.PublishRuntimeOptionDescription);

    public readonly Option<string> FrameworkOption = CommonOptions.CreateFrameworkOption(CommandDefinitionStrings.PublishFrameworkOptionDescription);

    public readonly Option<string?> ConfigurationOption = CommonOptions.CreateConfigurationOption(CommandDefinitionStrings.PublishConfigurationOptionDescription);
    public readonly Option<string[]> TargetOption = CreateTargetOption();

    public readonly Option<Utils.VerbosityOptions?> VerbosityOption = CommonOptions.CreateVerbosityOption();

    public readonly Option<bool> NoDependenciesOption = RestoreCommandDefinition.CreateNoDependenciesOption(showHelp: false);
    public readonly Option<string> ArtifactsPathOption = CommonOptions.CreateArtifactsPathOption();
    public readonly Option<string> VersionSuffixOption = CommonOptions.CreateVersionSuffixOption();
    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveMsBuildForwardOption();
    public readonly Option<bool> DisableBuildServersOption = CommonOptions.CreateDisableBuildServersOption();
    public readonly Option<string[]?> GetPropertyOption = CommonOptions.CreateGetPropertyOption();
    public readonly Option<string[]?> GetItemOption = CommonOptions.CreateGetItemOption();
    public readonly Option<string[]?> GetTargetResultOption = CommonOptions.CreateGetTargetResultOption();
    public readonly Option<string[]?> GetResultOutputFileOption = CommonOptions.CreateGetResultOutputFileOption();

    public PublishCommandDefinition()
        : base("publish", CommandDefinitionStrings.PublishAppDescription)
    {
        this.DocsLink = Link;

        Arguments.Add(SlnOrProjectOrFileArgument);
        ImplicitRestoreOptions.AddTo(Options);
        Options.Add(NoDependenciesOption);
        Options.Add(OutputOption);
        Options.Add(ArtifactsPathOption);
        Options.Add(ManifestOption);
        Options.Add(NoBuildOption);
        Options.Add(SelfContainedOption);
        Options.Add(NoSelfContainedOption);
        Options.Add(NoLogoOption);
        Options.Add(FrameworkOption);
        Options.Add(ConfigurationOption);
        Options.Add(VersionSuffixOption);
        Options.Add(InteractiveOption);
        Options.Add(NoRestoreOption);
        Options.Add(VerbosityOption);
        TargetPlatformOptions.AddTo(Options);
        Options.Add(DisableBuildServersOption);
        Options.Add(TargetOption);
        Options.Add(GetPropertyOption);
        Options.Add(GetItemOption);
        Options.Add(GetTargetResultOption);
        Options.Add(GetResultOutputFileOption);
    }

    public static Option<string[]> CreateTargetOption()
        => CommonOptions.CreateRequiredMSBuildTargetOption("Publish", [("_IsPublishing", "true")]);
}
