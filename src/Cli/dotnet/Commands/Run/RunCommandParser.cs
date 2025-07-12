// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Run;

internal static class RunCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-run";

    public static readonly Option<string?> ConfigurationOption = CommonOptions.ConfigurationOption(CliCommandStrings.RunConfigurationOptionDescription);

    public static readonly Option<string> FrameworkOption = CommonOptions.FrameworkOption(CliCommandStrings.RunFrameworkOptionDescription);

    public static readonly Option<string> RuntimeOption = CommonOptions.RuntimeOption(CliCommandStrings.RunRuntimeOptionDescription);

    public static readonly Option<string> ProjectOption = new("--project")
    {
        Description = CliCommandStrings.CommandOptionProjectDescription,
        HelpName = CliCommandStrings.CommandOptionProjectHelpName
    };

    public static readonly Option<ReadOnlyDictionary<string, string>?> PropertyOption = CommonOptions.PropertiesOption;

    public static readonly Option<string> LaunchProfileOption = new("--launch-profile", "-lp")
    {
        Description = CliCommandStrings.CommandOptionLaunchProfileDescription,
        HelpName = CliCommandStrings.CommandOptionLaunchProfileHelpName
    };

    public static readonly Option<bool> NoLaunchProfileOption = new("--no-launch-profile")
    {
        Description = CliCommandStrings.CommandOptionNoLaunchProfileDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<bool> NoLaunchProfileArgumentsOption = new("--no-launch-profile-arguments")
    {
        Description = CliCommandStrings.CommandOptionNoLaunchProfileArgumentsDescription
    };

    public static readonly Option<bool> NoBuildOption = new("--no-build")
    {
        Description = CliCommandStrings.CommandOptionNoBuildDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

    public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveMsBuildForwardOption;

    public static readonly Option<bool> NoCacheOption = new("--no-cache")
    {
        Description = CliCommandStrings.CommandOptionNoCacheDescription,
        Arity = ArgumentArity.Zero,
    };

    public static readonly Option SelfContainedOption = CommonOptions.SelfContainedOption;

    public static readonly Option NoSelfContainedOption = CommonOptions.NoSelfContainedOption;

    public static readonly Option<Utils.VerbosityOptions?> VerbosityOption = CommonOptions.VerbosityOption();

    public static readonly Argument<string[]> ApplicationArguments = new("applicationArguments")
    {
        DefaultValueFactory = _ => [],
        Description = "Arguments passed to the application that is being run."
    };

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        DocumentedCommand command = new("run", DocsLink, CliCommandStrings.RunAppFullName);

        command.Options.Add(ConfigurationOption);
        command.Options.Add(FrameworkOption);
        command.Options.Add(RuntimeOption);
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
        command.Options.Add(VerbosityOption);
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
