// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Build;

internal static class BuildCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-build";

    public static readonly Argument<string[]> SlnOrProjectOrFileArgument = new(CliStrings.SolutionOrProjectOrFileArgumentName)
    {
        Description = CliStrings.SolutionOrProjectOrFileArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public static readonly Option<string> OutputOption = new ForwardedOption<string>("--output", "-o")
    {
        Description = CliCommandStrings.BuildOutputOptionDescription,
        HelpName = CliCommandStrings.OutputOptionName
    }.ForwardAsOutputPath("OutputPath");

    public static readonly Option<bool> NoIncrementalOption = new ForwardedOption<bool>("--no-incremental")
    {
        Description = CliCommandStrings.NoIncrementalOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--target:Rebuild");

    public static readonly Option<bool> NoDependenciesOption = new ForwardedOption<bool>("--no-dependencies")
    {
        Description = CliCommandStrings.NoDependenciesOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--property:BuildProjectReferences=false");

    public static readonly Option<bool> NoLogoOption = new ForwardedOption<bool>("--nologo")
    {
        Description = CliCommandStrings.BuildCmdNoLogo,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-nologo");

    public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

    public static readonly Option<bool> SelfContainedOption = CommonOptions.SelfContainedOption;

    public static readonly Option<bool> NoSelfContainedOption = CommonOptions.NoSelfContainedOption;

    public static readonly Option<string> RuntimeOption = CommonOptions.RuntimeOption(CliCommandStrings.BuildRuntimeOptionDescription);

    public static readonly Option<string> FrameworkOption = CommonOptions.FrameworkOption(CliCommandStrings.BuildFrameworkOptionDescription);

    public static readonly Option<string?> ConfigurationOption = CommonOptions.ConfigurationOption(CliCommandStrings.BuildConfigurationOptionDescription);

    /// <summary>
    /// Build actually means 'run the default Target' generally in MSBuild
    /// </summary>
    public static readonly Option<string[]?> TargetOption = CommonOptions.MSBuildTargetOption();

    public static readonly Option<Utils.VerbosityOptions?> VerbosityOption = CommonOptions.VerbosityOption();

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        DocumentedCommand command = new("build", DocsLink, CliCommandStrings.BuildAppFullName);

        command.Arguments.Add(SlnOrProjectOrFileArgument);
        RestoreCommandParser.AddImplicitRestoreOptions(command, includeRuntimeOption: false, includeNoDependenciesOption: false);
        command.Options.Add(FrameworkOption);
        command.Options.Add(ConfigurationOption);
        command.Options.Add(RuntimeOption);
        command.Options.Add(CommonOptions.VersionSuffixOption);
        command.Options.Add(NoRestoreOption);
        command.Options.Add(CommonOptions.InteractiveMsBuildForwardOption);
        command.Options.Add(VerbosityOption);
        command.Options.Add(CommonOptions.DebugOption);
        command.Options.Add(OutputOption);
        command.Options.Add(CommonOptions.ArtifactsPathOption);
        command.Options.Add(NoIncrementalOption);
        command.Options.Add(NoDependenciesOption);
        command.Options.Add(NoLogoOption);
        command.Options.Add(SelfContainedOption);
        command.Options.Add(NoSelfContainedOption);
        command.Options.Add(CommonOptions.ArchitectureOption);
        command.Options.Add(CommonOptions.OperatingSystemOption);
        command.Options.Add(CommonOptions.DisableBuildServersOption);
        command.Options.Add(TargetOption);

        command.SetAction(BuildCommand.Run);

        return command;
    }
}
