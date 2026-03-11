// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Clean.FileBasedAppArtifacts;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Clean;

internal static class CleanCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-clean";

    public static readonly Argument<string[]> SlnOrProjectOrFileArgument = new(CliStrings.SolutionOrProjectOrFileArgumentName)
    {
        Description = CliStrings.SolutionOrProjectOrFileArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public static readonly Option<string> OutputOption = new ForwardedOption<string>("--output", "-o")
    {
        Description = CliCommandStrings.CleanCmdOutputDirDescription,
        HelpName = CliCommandStrings.CleanCmdOutputDir
    }.ForwardAsOutputPath("OutputPath");

    public static readonly Option<bool> NoLogoOption = new ForwardedOption<bool>("--nologo")
    {
        Description = CliCommandStrings.CleanCmdNoLogo,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-nologo");

    public static readonly Option FrameworkOption = CommonOptions.FrameworkOption(CliCommandStrings.CleanFrameworkOptionDescription);

    public static readonly Option ConfigurationOption = CommonOptions.ConfigurationOption(CliCommandStrings.CleanConfigurationOptionDescription);

    public static readonly Option<string[]> TargetOption = CommonOptions.RequiredMSBuildTargetOption("Clean");

    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = CommonOptions.VerbosityOption(Utils.VerbosityOptions.normal);


    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        DocumentedCommand command = new("clean", DocsLink, CliCommandStrings.CleanAppFullName);

        command.Arguments.Add(SlnOrProjectOrFileArgument);
        command.Options.Add(FrameworkOption);
        command.Options.Add(CommonOptions.RuntimeOption(CliCommandStrings.CleanRuntimeOptionDescription));
        command.Options.Add(ConfigurationOption);
        command.Options.Add(CommonOptions.InteractiveMsBuildForwardOption);
        command.Options.Add(VerbosityOption);
        command.Options.Add(OutputOption);
        command.Options.Add(CommonOptions.ArtifactsPathOption);
        command.Options.Add(NoLogoOption);
        command.Options.Add(CommonOptions.DisableBuildServersOption);
        command.Options.Add(TargetOption);
        command.Options.Add(CommonOptions.GetPropertyOption);
        command.Options.Add(CommonOptions.GetItemOption);
        command.Options.Add(CommonOptions.GetTargetResultOption);
        command.Options.Add(CommonOptions.GetResultOutputFileOption);
        command.Subcommands.Add(CleanFileBasedAppArtifactsCommandParser.Command);

        command.SetAction(CleanCommand.Run);

        return command;
    }
}
