// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Run;
using LocalizableStrings = Microsoft.DotNet.Tools.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RunCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-run";

        public static readonly Option<string> ConfigurationOption = CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription);

        public static readonly Option<string> FrameworkOption = CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription);

        public static readonly Option<string> RuntimeOption = CommonOptions.RuntimeOption;

        public static readonly Option<string> ProjectOption = new("--project")
        {
            Description = LocalizableStrings.CommandOptionProjectDescription
        };

        public static readonly Option<string[]> PropertyOption = CommonOptions.PropertiesOption;

        public static readonly Option<string> LaunchProfileOption = new("--launch-profile", "-lp")
        {
            Description = LocalizableStrings.CommandOptionLaunchProfileDescription
        };

        public static readonly Option<bool> NoLaunchProfileOption = new("--no-launch-profile")
        {
            Description = LocalizableStrings.CommandOptionNoLaunchProfileDescription
        };

        public static readonly Option<bool> NoLaunchProfileArgumentsOption = new("--no-launch-profile-arguments")
        {
            Description = LocalizableStrings.CommandOptionNoLaunchProfileArgumentsDescription
        };

        public static readonly Option<bool> NoBuildOption = new("--no-build")
        {
            Description = LocalizableStrings.CommandOptionNoBuildDescription
        };

        public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

        public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveMsBuildForwardOption;

        public static readonly Option SelfContainedOption = CommonOptions.SelfContainedOption;

        public static readonly Option NoSelfContainedOption = CommonOptions.NoSelfContainedOption;

        public static readonly Argument<string[]> ApplicationArguments = new("applicationArguments")
        {
            DefaultValueFactory = _ => Array.Empty<string>(),
            Description = "Arguments passed to the application that is being run."
        };

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            DocumentedCommand command = new("run", DocsLink, LocalizableStrings.AppFullName);

            command.Options.Add(ConfigurationOption);
            command.Options.Add(FrameworkOption);
            command.Options.Add(RuntimeOption.WithHelpDescription(command, LocalizableStrings.RuntimeOptionDescription));
            command.Options.Add(ProjectOption);
            command.Options.Add(PropertyOption);
            command.Options.Add(LaunchProfileOption);
            command.Options.Add(NoLaunchProfileOption);
            command.Options.Add(NoBuildOption);
            command.Options.Add(InteractiveOption);
            command.Options.Add(NoRestoreOption);
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
}
