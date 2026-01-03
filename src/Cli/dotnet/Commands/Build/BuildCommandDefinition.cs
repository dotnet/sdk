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

    public static Command Create()
    {
        Command command = new("build", CliCommandStrings.BuildAppFullName)
        {
            DocsLink = DocsLink
        };

        command.Arguments.Add(SlnOrProjectOrFileArgument);
        var implicitOptions = new ImplicitRestoreOptions(showHelp: false, useShortOptions: false);
        implicitOptions.AddTo(command.Options);
        command.Options.Add(FrameworkOption);
        command.Options.Add(ConfigurationOption);
        command.Options.Add(RuntimeOption);
        command.Options.Add(CommonOptions.CreateVersionSuffixOption());
        command.Options.Add(NoRestoreOption);
        command.Options.Add(CommonOptions.CreateInteractiveMsBuildForwardOption());
        command.Options.Add(VerbosityOption);
        command.Options.Add(CommonOptions.DebugOption);
        command.Options.Add(OutputOption);
        command.Options.Add(CommonOptions.CreateArtifactsPathOption());
        command.Options.Add(NoIncrementalOption);
        command.Options.Add(NoDependenciesOption);
        command.Options.Add(NoLogoOption);
        command.Options.Add(SelfContainedOption);
        command.Options.Add(NoSelfContainedOption);
        command.Options.Add(CommonOptions.ArchitectureOption);
        command.Options.Add(CommonOptions.OperatingSystemOption);
        command.Options.Add(CommonOptions.CreateDisableBuildServersOption());
        command.Options.Add(TargetOption);
        command.Options.Add(CommonOptions.CreateGetPropertyOption());
        command.Options.Add(CommonOptions.CreateGetItemOption());
        command.Options.Add(CommonOptions.CreateGetTargetResultOption());
        command.Options.Add(CommonOptions.CreateGetResultOutputFileOption());

        return command;
    }
}
