// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Clean;

internal static class CleanCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-clean";

    public static readonly CliArgument<IEnumerable<string>> SlnOrProjectArgument = new(CliStrings.SolutionOrProjectArgumentName)
    {
        Description = CliStrings.SolutionOrProjectArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public static readonly CliOption<string> OutputOption = new ForwardedOption<string>("--output", "-o")
    {
        Description = CliCommandStrings.CleanCmdOutputDirDescription,
        HelpName = CliCommandStrings.CleanCmdOutputDir
    }.ForwardAsOutputPath("OutputPath");

    public static readonly CliOption<bool> NoLogoOption = new ForwardedOption<bool>("--nologo")
    {
        Description = CliCommandStrings.CleanCmdNoLogo,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-nologo");

    public static readonly CliOption FrameworkOption = CommonOptions.FrameworkOption(CliCommandStrings.CleanFrameworkOptionDescription);

    public static readonly CliOption ConfigurationOption = CommonOptions.ConfigurationOption(CliCommandStrings.CleanConfigurationOptionDescription);

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        DocumentedCommand command = new("clean", DocsLink, CliCommandStrings.CleanAppFullName);

        command.Arguments.Add(SlnOrProjectArgument);
        command.Options.Add(FrameworkOption);
        command.Options.Add(CommonOptions.RuntimeOption.WithHelpDescription(command, CliCommandStrings.CleanRuntimeOptionDescription));
        command.Options.Add(ConfigurationOption);
        command.Options.Add(CommonOptions.InteractiveMsBuildForwardOption);
        command.Options.Add(CommonOptions.VerbosityOption);
        command.Options.Add(OutputOption);
        command.Options.Add(CommonOptions.ArtifactsPathOption);
        command.Options.Add(NoLogoOption);
        command.Options.Add(CommonOptions.DisableBuildServersOption);

        command.SetAction(CleanCommand.Run);

        return command;
    }
}
