// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Build;

internal static class BuildCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-build";

    public static readonly CliArgument<IEnumerable<string>> SlnOrProjectArgument = new(CommonLocalizableStrings.SolutionOrProjectArgumentName)
    {
        Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public static readonly CliOption<string> OutputOption = new ForwardedOption<string>("--output", "-o")
    {
        Description = LocalizableStrings.BuildOutputOptionDescription,
        HelpName = LocalizableStrings.OutputOptionName
    }.ForwardAsOutputPath("OutputPath");

    public static readonly CliOption<bool> NoIncrementalOption = new("--no-incremental")
    {
        Description = LocalizableStrings.NoIncrementalOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly CliOption<bool> NoDependenciesOption = new ForwardedOption<bool>("--no-dependencies")
    {
        Description = LocalizableStrings.NoDependenciesOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:BuildProjectReferences=false");

    public static readonly CliOption<bool> NoLogoOption = new ForwardedOption<bool>("--nologo")
    {
        Description = LocalizableStrings.BuildCmdNoLogo,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-nologo");

    public static readonly CliOption<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

    public static readonly CliOption<bool> SelfContainedOption = CommonOptions.SelfContainedOption;

    public static readonly CliOption<bool> NoSelfContainedOption = CommonOptions.NoSelfContainedOption;

    public static readonly CliOption<string> RuntimeOption = CommonOptions.RuntimeOption;

    public static readonly CliOption<string> FrameworkOption = CommonOptions.FrameworkOption(LocalizableStrings.BuildFrameworkOptionDescription);

    public static readonly CliOption<string> ConfigurationOption = CommonOptions.ConfigurationOption(LocalizableStrings.BuildConfigurationOptionDescription);

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        DocumentedCommand command = new("build", DocsLink, LocalizableStrings.BuildAppFullName);

        command.Arguments.Add(SlnOrProjectArgument);
        RestoreCommandParser.AddImplicitRestoreOptions(command, includeRuntimeOption: false, includeNoDependenciesOption: false);
        command.Options.Add(FrameworkOption);
        command.Options.Add(ConfigurationOption);
        command.Options.Add(RuntimeOption.WithHelpDescription(command, LocalizableStrings.BuildRuntimeOptionDescription));
        command.Options.Add(CommonOptions.VersionSuffixOption);
        command.Options.Add(NoRestoreOption);
        command.Options.Add(CommonOptions.InteractiveMsBuildForwardOption);
        command.Options.Add(CommonOptions.VerbosityOption);
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

        command.SetAction(BuildCommand.Run);

        return command;
    }
}
