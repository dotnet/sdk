// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Run;

internal sealed class RunCommandDefinition : Command
{
    private const string Link = "https://aka.ms/dotnet-run";

    public readonly Option<string?> ConfigurationOption = CommonOptions.CreateConfigurationOption(CommandDefinitionStrings.RunConfigurationOptionDescription);

    public readonly Option<string> FrameworkOption = CommonOptions.CreateFrameworkOption(CommandDefinitionStrings.RunFrameworkOptionDescription);

    public readonly TargetPlatformOptions TargetPlatformOptions = new(CommandDefinitionStrings.RunRuntimeOptionDescription);

    public readonly Option<string?> ProjectOption = new("--project", "-p")
    {
        Description = CommandDefinitionStrings.CmdProjectDescriptionFormat,
        HelpName = CommandDefinitionStrings.CommandOptionProjectHelpName,
        Arity = ArgumentArity.ZeroOrMore,
        AllowMultipleArgumentsPerToken = false,
        CustomParser = static result =>
        {
            foreach (var token in result.Tokens)
            {
                if (!token.Value.Contains('='))
                {
                    return token.Value;
                }
            }

            return null;
        }
    };

    public readonly Option<string> FileOption = new("--file")
    {
        Description = CommandDefinitionStrings.CommandOptionFileDescription,
        HelpName = CommandDefinitionStrings.CommandOptionFileHelpName,
    };

    public readonly Option<ReadOnlyDictionary<string, string>?> PropertyOption = CommonOptions.CreatePropertyOption(includeShortAlias: false);

    public readonly Option<string> LaunchProfileOption = new("--launch-profile", "-lp")
    {
        Description = CommandDefinitionStrings.CommandOptionLaunchProfileDescription,
        HelpName = CommandDefinitionStrings.CommandOptionLaunchProfileHelpName
    };

    public readonly Option<bool> NoLaunchProfileOption = new("--no-launch-profile")
    {
        Description = CommandDefinitionStrings.CommandOptionNoLaunchProfileDescription,
        Arity = ArgumentArity.Zero
    };

    public readonly Option<bool> NoLaunchProfileArgumentsOption = new("--no-launch-profile-arguments")
    {
        Description = CommandDefinitionStrings.CommandOptionNoLaunchProfileArgumentsDescription
    };

    public readonly Option<string> DeviceOption = new("--device")
    {
        Description = CommandDefinitionStrings.CommandOptionDeviceDescription,
        HelpName = CommandDefinitionStrings.CommandOptionDeviceHelpName
    };

    public readonly Option<bool> ListDevicesOption = new("--list-devices")
    {
        Description = CommandDefinitionStrings.CommandOptionListDevicesDescription,
        Arity = ArgumentArity.Zero
    };

    public const string NoBuildOptionName = "--no-build";

    public readonly Option<bool> NoBuildOption = new(NoBuildOptionName)
    {
        Description = CommandDefinitionStrings.CommandOptionNoBuildDescription,
        Arity = ArgumentArity.Zero
    };

    public readonly Option<bool> NoRestoreOption = CommonOptions.CreateNoRestoreOption();

    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveMsBuildForwardOption();

    public const string NoCacheOptionName = "--no-cache";

    public readonly Option<bool> NoCacheOption = new(NoCacheOptionName)
    {
        Description = CommandDefinitionStrings.CommandOptionNoCacheDescription,
        Arity = ArgumentArity.Zero,
    };

    public readonly Option<bool> SelfContainedOption = CommonOptions.CreateSelfContainedOption();

    public readonly Option<bool> NoSelfContainedOption = CommonOptions.CreateNoSelfContainedOption();

    public readonly Option<Utils.VerbosityOptions?> VerbosityOption = CommonOptions.CreateVerbosityOption();

    public readonly Option<bool> DisableBuildServersOption = CommonOptions.CreateDisableBuildServersOption();

    public readonly Option<string> ArtifactsPathOption = CommonOptions.CreateArtifactsPathOption();

    public readonly Option<IReadOnlyDictionary<string, string>> EnvOption = CommonOptions.CreateEnvOption();

    public readonly Argument<string[]> ApplicationArguments = new("applicationArguments")
    {
        DefaultValueFactory = _ => [],
        Description = "Arguments passed to the application that is being run."
    };

    public RunCommandDefinition()
        : base("run", CommandDefinitionStrings.RunAppFullName)
    {
        this.DocsLink = Link;

        Options.Add(ConfigurationOption);
        Options.Add(FrameworkOption);
        Options.Add(ProjectOption);
        Options.Add(FileOption);
        Options.Add(PropertyOption);
        Options.Add(LaunchProfileOption);
        Options.Add(NoLaunchProfileOption);
        Options.Add(DeviceOption);
        Options.Add(ListDevicesOption);
        Options.Add(NoBuildOption);
        Options.Add(InteractiveOption);
        Options.Add(NoRestoreOption);
        Options.Add(NoCacheOption);
        Options.Add(SelfContainedOption);
        Options.Add(NoSelfContainedOption);
        Options.Add(VerbosityOption);
        TargetPlatformOptions.AddTo(Options);
        Options.Add(DisableBuildServersOption);
        Options.Add(ArtifactsPathOption);
        Options.Add(EnvOption);

        Arguments.Add(ApplicationArguments);
    }

    /// <summary>
    /// Separates <c>--project</c> and <c>-p</c> values into project paths and MSBuild property arguments.
    /// A <c>-p</c> value that contains <c>=</c> is a property. <c>--project</c> values are always project paths.
    /// </summary>
    internal static (IReadOnlyList<string> Projects, IReadOnlyList<string> PropertyArguments) SplitProjectOption(ParseResult parseResult)
    {
        if (parseResult.CommandResult.Command is not RunCommandDefinition definition)
        {
            return ([], []);
        }

        List<string> projects = [];
        List<string> propertyArguments = [];

        foreach (var result in parseResult.CommandResult.Children.OfType<OptionResult>())
        {
            if (result.Option != definition.ProjectOption)
            {
                continue;
            }

            var identifier = result.IdentifierToken?.Value ?? string.Empty;
            var shortForm = identifier.Length == 0
                || identifier == "-p"
                || identifier.StartsWith("-p:", StringComparison.Ordinal)
                || identifier.StartsWith("-p=", StringComparison.Ordinal);

            if (result.Tokens.Count == 0 && shortForm && TryGetAttachedOptionValue(identifier, "-p", out var attachedValue))
            {
                ClassifyShortProjectArgument(attachedValue, projects, propertyArguments);
                continue;
            }

            foreach (var token in result.Tokens)
            {
                if (shortForm)
                {
                    ClassifyShortProjectArgument(token.Value, projects, propertyArguments);
                }
                else
                {
                    projects.Add(token.Value);
                }
            }
        }

        return (projects, propertyArguments);

        static bool TryGetAttachedOptionValue(string identifier, string optionName, out string value)
        {
            if (identifier.StartsWith(optionName + ":", StringComparison.Ordinal) ||
                identifier.StartsWith(optionName + "=", StringComparison.Ordinal))
            {
                value = identifier[(optionName.Length + 1)..];
                return true;
            }

            value = string.Empty;
            return false;
        }

        static void ClassifyShortProjectArgument(string argument, List<string> projects, List<string> propertyArguments)
        {
            if (!argument.Contains('='))
            {
                projects.Add(argument);
                return;
            }

            foreach (var (key, propertyValue) in MSBuildPropertyParser.ParseProperties(argument))
            {
                propertyArguments.Add($"--property:{key}={propertyValue}");
            }
        }
    }
}
