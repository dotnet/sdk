// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Restore;

namespace Microsoft.DotNet.Cli.Commands.Build;

internal sealed class BuildCommandDefinition : Command
{
    private const string Link = "https://aka.ms/dotnet-build";

    public readonly Argument<string[]> SlnOrProjectOrFileArgument = new(CommandDefinitionStrings.SolutionOrProjectOrFileArgumentName)
    {
        Description = CommandDefinitionStrings.SolutionOrProjectOrFileArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public readonly ImplicitRestoreOptions ImplicitRestoreOptions = new(showHelp: false, useShortOptions: false);

    public readonly Option<string> OutputOption = new Option<string>("--output", "-o")
    {
        Description = CommandDefinitionStrings.BuildOutputOptionDescription,
        HelpName = CommandDefinitionStrings.OutputOptionName
    }.ForwardAsOutputPath("OutputPath");

    public readonly Option<bool> NoIncrementalOption = new Option<bool>("--no-incremental")
    {
        Description = CommandDefinitionStrings.NoIncrementalOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--target:Rebuild");

    public readonly Option<bool> NoDependenciesOption = new Option<bool>("--no-dependencies")
    {
        Description = CommandDefinitionStrings.NoDependenciesOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--property:BuildProjectReferences=false");

    public readonly Option<bool> NoLogoOption = CommonOptions.CreateNoLogoOption();

    public readonly Option<bool> NoRestoreOption = CommonOptions.CreateNoRestoreOption();

    public readonly Option<bool> SelfContainedOption = CommonOptions.CreateSelfContainedOption();

    public readonly Option<bool> NoSelfContainedOption = CommonOptions.CreateNoSelfContainedOption();

    public readonly TargetPlatformOptions TargetPlatformOptions = new(CommandDefinitionStrings.BuildRuntimeOptionDescription);

    public readonly Option<string> FrameworkOption = CommonOptions.CreateFrameworkOption(CommandDefinitionStrings.BuildFrameworkOptionDescription);

    public readonly Option<string?> ConfigurationOption = CommonOptions.CreateConfigurationOption(CommandDefinitionStrings.BuildConfigurationOptionDescription);

    /// <summary>
    /// Build actually means 'run the default Target' generally in MSBuild
    /// </summary>
    public readonly Option<string[]?> TargetOption = CommonOptions.CreateMSBuildTargetOption();

    public readonly Option<Utils.VerbosityOptions?> VerbosityOption = CommonOptions.CreateVerbosityOption();

    public readonly Option<string> VersionSuffixOption = CommonOptions.CreateVersionSuffixOption();
    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveMsBuildForwardOption();
    public readonly Option<string> ArtifactsPathOption = CommonOptions.CreateArtifactsPathOption();
    public readonly Option<bool> DisableBuildServersOption = CommonOptions.CreateDisableBuildServersOption();
    public readonly Option<string[]?> GetPropertyOption = CommonOptions.CreateGetPropertyOption();
    public readonly Option<string[]?> GetItemOption = CommonOptions.CreateGetItemOption();
    public readonly Option<string[]?> GetTargetResultOption = CommonOptions.CreateGetTargetResultOption();
    public readonly Option<string[]?> GetResultOutputFileOption = CommonOptions.CreateGetResultOutputFileOption();

    public BuildCommandDefinition()
        : base("build", CommandDefinitionStrings.BuildAppFullName)
    {
        this.DocsLink = Link;

        Arguments.Add(SlnOrProjectOrFileArgument);
        ImplicitRestoreOptions.AddTo(Options);
        Options.Add(FrameworkOption);
        Options.Add(ConfigurationOption);
        Options.Add(VersionSuffixOption);
        Options.Add(NoRestoreOption);
        Options.Add(InteractiveOption);
        Options.Add(VerbosityOption);
        Options.Add(OutputOption);
        Options.Add(ArtifactsPathOption);
        Options.Add(NoIncrementalOption);
        Options.Add(NoDependenciesOption);
        Options.Add(NoLogoOption);
        Options.Add(SelfContainedOption);
        Options.Add(NoSelfContainedOption);
        TargetPlatformOptions.AddTo(Options);
        Options.Add(DisableBuildServersOption);
        Options.Add(TargetOption);
        Options.Add(GetPropertyOption);
        Options.Add(GetItemOption);
        Options.Add(GetTargetResultOption);
        Options.Add(GetResultOutputFileOption);
    }
}
