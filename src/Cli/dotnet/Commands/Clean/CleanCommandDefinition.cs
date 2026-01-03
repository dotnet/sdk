// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Clean.FileBasedAppArtifacts;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Clean;

internal static class CleanCommandDefinition
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-clean";

    public static readonly Argument<string[]> SlnOrProjectOrFileArgument = new(CliStrings.SolutionOrProjectOrFileArgumentName)
    {
        Description = CliStrings.SolutionOrProjectOrFileArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public static readonly Option<string> OutputOption = new Option<string>("--output", "-o")
    {
        Description = CliCommandStrings.CleanCmdOutputDirDescription,
        HelpName = CliCommandStrings.CleanCmdOutputDir
    }.ForwardAsOutputPath("OutputPath");

    public static readonly Option<bool> NoLogoOption = CommonOptions.CreateNoLogoOption();

    public static readonly Option<string> FrameworkOption = CommonOptions.CreateFrameworkOption(CliCommandStrings.CleanFrameworkOptionDescription);

    public static readonly Option<string?> ConfigurationOption = CommonOptions.CreateConfigurationOption(CliCommandStrings.CleanConfigurationOptionDescription);

    public static readonly Option<string[]> TargetOption = CommonOptions.CreateRequiredMSBuildTargetOption("Clean");

    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = CommonOptions.CreateVerbosityOption(Utils.VerbosityOptions.normal);

    public static readonly Option<string> RuntimeOption = CommonOptions.CreateRuntimeOption(CliCommandStrings.CleanRuntimeOptionDescription);
    public static readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveMsBuildForwardOption();
    public static readonly Option<string> ArtifactsPathOption = CommonOptions.CreateArtifactsPathOption();
    public static readonly Option<bool> DisableBuildServersOption = CommonOptions.CreateDisableBuildServersOption();
    public static readonly Option<string[]?> GetPropertyOption = CommonOptions.CreateGetPropertyOption();
    public static readonly Option<string[]?> GetItemOption = CommonOptions.CreateGetItemOption();
    public static readonly Option<string[]?> GetTargetResultOption = CommonOptions.CreateGetTargetResultOption();
    public static readonly Option<string[]?> GetResultOutputFileOption = CommonOptions.CreateGetResultOutputFileOption();

    public static Command Create()
    {
        Command command = new("clean", CliCommandStrings.CleanAppFullName)
        {
            DocsLink = DocsLink
        };

        command.Arguments.Add(SlnOrProjectOrFileArgument);
        command.Options.Add(FrameworkOption);
        command.Options.Add(RuntimeOption);
        command.Options.Add(ConfigurationOption);
        command.Options.Add(InteractiveOption);
        command.Options.Add(VerbosityOption);
        command.Options.Add(OutputOption);
        command.Options.Add(ArtifactsPathOption);
        command.Options.Add(NoLogoOption);
        command.Options.Add(DisableBuildServersOption);
        command.Options.Add(TargetOption);
        command.Options.Add(GetPropertyOption);
        command.Options.Add(GetItemOption);
        command.Options.Add(GetTargetResultOption);
        command.Options.Add(GetResultOutputFileOption);
        command.Subcommands.Add(CleanFileBasedAppArtifactsCommandDefinition.Create());

        return command;
    }
}
