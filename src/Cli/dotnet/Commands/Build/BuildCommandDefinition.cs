// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Build;

internal static class BuildCommandDefinition
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-build";

    public static readonly Argument<string[]> SlnOrProjectOrFileArgument = new(CliStrings.SolutionOrProjectOrFileArgumentName)
    {
        Description = CliStrings.SolutionOrProjectOrFileArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public static readonly ImplicitRestoreOptions ImplicitRestoreOptions = new(showHelp: false, useShortOptions: false);

    public static readonly Option<string> OutputOption = new Option<string>("--output", "-o")
    {
        Description = CliCommandStrings.BuildOutputOptionDescription,
        HelpName = CliCommandStrings.OutputOptionName
    }.ForwardAsOutputPath("OutputPath");

    public static readonly Option<bool> NoIncrementalOption = new Option<bool>("--no-incremental")
    {
        Description = CliCommandStrings.NoIncrementalOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--target:Rebuild");

    public static readonly Option<bool> NoDependenciesOption = new Option<bool>("--no-dependencies")
    {
        Description = CliCommandStrings.NoDependenciesOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--property:BuildProjectReferences=false");

    public static readonly Option<bool> NoLogoOption = CommonOptions.CreateNoLogoOption();

    public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

    public static readonly Option<bool> SelfContainedOption = CommonOptions.SelfContainedOption;

    public static readonly Option<bool> NoSelfContainedOption = CommonOptions.NoSelfContainedOption;

    public static readonly Option<string> RuntimeOption = CommonOptions.CreateRuntimeOption(CliCommandStrings.BuildRuntimeOptionDescription);

    public static readonly Option<string> FrameworkOption = CommonOptions.CreateFrameworkOption(CliCommandStrings.BuildFrameworkOptionDescription);

    public static readonly Option<string?> ConfigurationOption = CommonOptions.CreateConfigurationOption(CliCommandStrings.BuildConfigurationOptionDescription);

    /// <summary>
    /// Build actually means 'run the default Target' generally in MSBuild
    /// </summary>
    public static readonly Option<string[]?> TargetOption = CommonOptions.CreateMSBuildTargetOption();

    public static readonly Option<Utils.VerbosityOptions?> VerbosityOption = CommonOptions.CreateVerbosityOption();

    public static readonly Option<string> VersionSuffixOption = CommonOptions.CreateVersionSuffixOption();
    public static readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveMsBuildForwardOption();
    public static readonly Option<bool> DebugOption = CommonOptions.DebugOption;
    public static readonly Option<string> ArtifactsPathOption = CommonOptions.CreateArtifactsPathOption();
    public static readonly Option<string> ArchitectureOption = CommonOptions.ArchitectureOption;
    public static readonly Option<string> OperatingSystemOption = CommonOptions.OperatingSystemOption;
    public static readonly Option<bool> DisableBuildServersOption = CommonOptions.CreateDisableBuildServersOption();
    public static readonly Option<string[]?> GetPropertyOption = CommonOptions.CreateGetPropertyOption();
    public static readonly Option<string[]?> GetItemOption = CommonOptions.CreateGetItemOption();
    public static readonly Option<string[]?> GetTargetResultOption = CommonOptions.CreateGetTargetResultOption();
    public static readonly Option<string[]?> GetResultOutputFileOption = CommonOptions.CreateGetResultOutputFileOption();

    public static Command Create()
    {
        Command command = new("build", CliCommandStrings.BuildAppFullName)
        {
            DocsLink = DocsLink
        };

        command.Arguments.Add(SlnOrProjectOrFileArgument);
        ImplicitRestoreOptions.AddTo(command.Options);
        command.Options.Add(FrameworkOption);
        command.Options.Add(ConfigurationOption);
        command.Options.Add(RuntimeOption);
        command.Options.Add(VersionSuffixOption);
        command.Options.Add(NoRestoreOption);
        command.Options.Add(InteractiveOption);
        command.Options.Add(VerbosityOption);
        command.Options.Add(DebugOption);
        command.Options.Add(OutputOption);
        command.Options.Add(ArtifactsPathOption);
        command.Options.Add(NoIncrementalOption);
        command.Options.Add(NoDependenciesOption);
        command.Options.Add(NoLogoOption);
        command.Options.Add(SelfContainedOption);
        command.Options.Add(NoSelfContainedOption);
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
