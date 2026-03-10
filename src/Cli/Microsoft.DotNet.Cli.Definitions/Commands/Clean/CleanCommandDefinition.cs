// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Clean.FileBasedAppArtifacts;

namespace Microsoft.DotNet.Cli.Commands.Clean;

internal sealed class CleanCommandDefinition : Command
{
    public new const string Name = "clean";
    private const string Link = "https://aka.ms/dotnet-clean";

    public readonly Argument<string[]> SlnOrProjectOrFileArgument = new(CommandDefinitionStrings.SolutionOrProjectOrFileArgumentName)
    {
        Description = CommandDefinitionStrings.SolutionOrProjectOrFileArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public readonly Option<string> OutputOption = new Option<string>("--output", "-o")
    {
        Description = CommandDefinitionStrings.CleanCmdOutputDirDescription,
        HelpName = CommandDefinitionStrings.CleanCmdOutputDir
    }.ForwardAsOutputPath("OutputPath");

    public readonly Option<bool> NoLogoOption = CommonOptions.CreateNoLogoOption();

    public readonly Option<string> FrameworkOption = CommonOptions.CreateFrameworkOption(CommandDefinitionStrings.CleanFrameworkOptionDescription);

    public readonly Option<string?> ConfigurationOption = CommonOptions.CreateConfigurationOption(CommandDefinitionStrings.CleanConfigurationOptionDescription);

    public readonly Option<string[]> TargetOption = CreateTargetOption();

    public readonly Option<Utils.VerbosityOptions> VerbosityOption = CommonOptions.CreateVerbosityOption(Utils.VerbosityOptions.normal);

    public readonly Option<string> RuntimeOption = TargetPlatformOptions.CreateRuntimeOption(CommandDefinitionStrings.CleanRuntimeOptionDescription);
    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveMsBuildForwardOption();
    public readonly Option<string> ArtifactsPathOption = CommonOptions.CreateArtifactsPathOption();
    public readonly Option<bool> DisableBuildServersOption = CommonOptions.CreateDisableBuildServersOption();
    public readonly Option<string[]?> GetPropertyOption = CommonOptions.CreateGetPropertyOption();
    public readonly Option<string[]?> GetItemOption = CommonOptions.CreateGetItemOption();
    public readonly Option<string[]?> GetTargetResultOption = CommonOptions.CreateGetTargetResultOption();
    public readonly Option<string[]?> GetResultOutputFileOption = CommonOptions.CreateGetResultOutputFileOption();

    public readonly Command FileBasedAppsCommand = new CleanFileBasedAppArtifactsCommandDefinition();

    public CleanCommandDefinition()
        : base(Name, CommandDefinitionStrings.CleanAppFullName)
    {
        this.DocsLink = Link;

        Arguments.Add(SlnOrProjectOrFileArgument);
        Options.Add(FrameworkOption);
        Options.Add(RuntimeOption);
        Options.Add(ConfigurationOption);
        Options.Add(InteractiveOption);
        Options.Add(VerbosityOption);
        Options.Add(OutputOption);
        Options.Add(ArtifactsPathOption);
        Options.Add(NoLogoOption);
        Options.Add(DisableBuildServersOption);
        Options.Add(TargetOption);
        Options.Add(GetPropertyOption);
        Options.Add(GetItemOption);
        Options.Add(GetTargetResultOption);
        Options.Add(GetResultOutputFileOption);

        Subcommands.Add(FileBasedAppsCommand);
    }

    public static Option<string[]> CreateTargetOption()
        => CommonOptions.CreateRequiredMSBuildTargetOption("Clean");
}
