// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Run;

internal static class RunCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-run";

    public static readonly CliOption<string> ConfigurationOption = CommonOptions.ConfigurationOption(CliCommandStrings.RunConfigurationOptionDescription);

    public static readonly CliOption<string> FrameworkOption = CommonOptions.FrameworkOption(CliCommandStrings.RunFrameworkOptionDescription);

    public static readonly CliOption<string> RuntimeOption = CommonOptions.RuntimeOption;

    public static readonly CliOption<string> ProjectOption = new("--project")
    {
        Description = CliCommandStrings.CommandOptionProjectDescription
    };

    public static readonly CliOption<string[]> PropertyOption = CommonOptions.PropertiesOption;

    public static readonly CliOption<string> LaunchProfileOption = new("--launch-profile", "-lp")
    {
        Description = CliCommandStrings.CommandOptionLaunchProfileDescription
    };

    public static readonly CliOption<bool> NoLaunchProfileOption = new("--no-launch-profile")
    {
        Description = CliCommandStrings.CommandOptionNoLaunchProfileDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly CliOption<bool> NoLaunchProfileArgumentsOption = new("--no-launch-profile-arguments")
    {
        Description = CliCommandStrings.CommandOptionNoLaunchProfileArgumentsDescription
    };

    public static readonly CliOption<bool> NoBuildOption = new("--no-build")
    {
        Description = CliCommandStrings.CommandOptionNoBuildDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly CliOption<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

    public static readonly CliOption<bool> NoCacheOption = new("--no-cache")
    {
        Description = CliCommandStrings.CommandOptionNoCacheDescription,
        Arity = ArgumentArity.Zero,
    };

    public static readonly CliOption<bool> InteractiveOption = CommonOptions.InteractiveMsBuildForwardOption;

    public static readonly CliOption SelfContainedOption = CommonOptions.SelfContainedOption;

    public static readonly CliOption NoSelfContainedOption = CommonOptions.NoSelfContainedOption;

    public static readonly CliArgument<string[]> ApplicationArguments = new("applicationArguments")
    {
        DefaultValueFactory = _ => [],
        Description = "Arguments passed to the application that is being run."
    };

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        DocumentedCommand command = new("run", DocsLink, CliCommandStrings.RunAppFullName);

        command.Options.Add(ConfigurationOption);
        command.Options.Add(FrameworkOption);
        command.Options.Add(RuntimeOption.WithHelpDescription(command, CliCommandStrings.RunRuntimeOptionDescription));
        command.Options.Add(ProjectOption);
        command.Options.Add(PropertyOption);
        command.Options.Add(LaunchProfileOption);
        command.Options.Add(NoLaunchProfileOption);
        command.Options.Add(NoBuildOption);
        command.Options.Add(InteractiveOption);
        command.Options.Add(NoRestoreOption);
        command.Options.Add(NoCacheOption);
        command.Options.Add(SelfContainedOption);
        command.Options.Add(NoSelfContainedOption);
        command.Options.Add(CommonOptions.VerbosityOption);
        command.Options.Add(CommonOptions.ArchitectureOption);
        command.Options.Add(CommonOptions.OperatingSystemOption);
        command.Options.Add(CommonOptions.DisableBuildServersOption);
        command.Options.Add(CommonOptions.ArtifactsPathOption);
        command.Options.Add(CommonOptions.EnvOption);

        command.Arguments.Add(ApplicationArguments);

        command.SetAction(RunCommand.Run);

        return command;
    }
}
