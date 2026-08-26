// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Help;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal abstract partial class TestCommandDefinition
{
    public sealed partial class MicrosoftTestingPlatform : TestCommandDefinition, ICustomHelp
    {
        private const long MaxSupportedTimeoutMilliseconds = 0xfffffffe;

        public readonly Option<string> ProjectOrSolutionOption = new("--project")
        {
            Description = CommandDefinitionStrings.CmdProjectOrSolutionDescriptionFormat,
            HelpName = CommandDefinitionStrings.CmdProjectOrSolutionPathName,
            Arity = ArgumentArity.ExactlyOne
        };

        public readonly Option<string> SolutionOption = new("--solution")
        {
            Description = CommandDefinitionStrings.CmdSolutionDescription,
            HelpName = CommandDefinitionStrings.CmdSolutionPathName,
            Arity = ArgumentArity.ExactlyOne
        };

        public readonly Option<string> TestModulesFilterOption = new("--test-modules")
        {
            Description = CommandDefinitionStrings.CmdTestModulesDescription,
            HelpName = CommandDefinitionStrings.CmdExpressionName
        };

        public readonly Option<string> TestModulesRootDirectoryOption = new("--root-directory")
        {
            Description = CommandDefinitionStrings.CmdTestModulesRootDirectoryDescription,
            HelpName = CommandDefinitionStrings.CmdRootPathName,
        };

        public const string ResultsDirectoryOptionName = "--results-directory";

        public readonly Option<string> ResultsDirectoryOption = new(ResultsDirectoryOptionName)
        {
            Description = CommandDefinitionStrings.CmdResultsDirectoryDescription,
            HelpName = CommandDefinitionStrings.CmdPathToResultsDirectory,
            Arity = ArgumentArity.ExactlyOne
        };

        public readonly Option<string> ResultsDirectoryLayoutOption = new Option<string>("--results-directory-layout")
        {
            Description = CommandDefinitionStrings.CmdResultsDirectoryLayoutDescription,
            HelpName = CommandDefinitionStrings.CmdResultsDirectoryLayoutName,
            Arity = ArgumentArity.ExactlyOne
        }.AcceptOnlyFromAmong("flat", "per-module");

        public const string ConfigFileOptionName = "--config-file";

        public readonly Option<string> ConfigFileOption = new(ConfigFileOptionName)
        {
            Description = CommandDefinitionStrings.CmdConfigFileDescription,
            HelpName = CommandDefinitionStrings.CmdConfigFilePath,
            Arity = ArgumentArity.ExactlyOne
        };

        public const string DiagnosticOutputDirectoryOptionName = "--diagnostic-output-directory";

        public readonly Option<string> DiagnosticOutputDirectoryOption = new(DiagnosticOutputDirectoryOptionName)
        {
            Description = CommandDefinitionStrings.CmdDiagnosticOutputDirectoryDescription,
            HelpName = CommandDefinitionStrings.CmdDiagnosticOutputDirectoryPath,
            Arity = ArgumentArity.ExactlyOne
        };

        public readonly Option<int> MaxParallelTestModulesOption = new("--max-parallel-test-modules")
        {
            Description = CommandDefinitionStrings.CmdMaxParallelTestModulesDescription,
            HelpName = CommandDefinitionStrings.CmdNumberName
        };

        public readonly Option<int> MinimumExpectedTestsOption = new("--minimum-expected-tests")
        {
            Description = CommandDefinitionStrings.CmdMinimumExpectedTestsDescription,
            HelpName = CommandDefinitionStrings.CmdNumberName
        };

        public readonly Option<int?> MaximumFailedTestsOption = new("--maximum-failed-tests")
        {
            Description = CommandDefinitionStrings.CmdMaximumFailedTestsDescription,
            HelpName = CommandDefinitionStrings.CmdNumberName,
            Arity = ArgumentArity.ExactlyOne
        };

        public readonly Option<TimeSpan?> TimeoutOption = new("--timeout")
        {
            Description = CommandDefinitionStrings.CmdTimeoutDescription,
            HelpName = CommandDefinitionStrings.CmdDurationName,
            Arity = ArgumentArity.ExactlyOne,
            CustomParser = ParseTimeout
        };

        public readonly Option<IReadOnlyDictionary<string, string>> EnvOption = CommonOptions.CreateEnvOption();

        public readonly Option<ReadOnlyDictionary<string, string>?> PropertiesOption = CommonOptions.CreatePropertyOption();

        public readonly Option<bool> NoRestoreOption = CommonOptions.CreateNoRestoreOption();

        public readonly Option<bool> NoBuildOption = new("--no-build")
        {
            Description = CommandDefinitionStrings.CmdNoBuildDescription
        };

        public readonly Option<bool> NoLogoOption = CommonOptions.CreateNoLogoOption(
            defaultValue: false,
            forwardAs: null,
            description: CommandDefinitionStrings.TestCmdNoLogo);

        public readonly Option<bool> UseCurrentRuntimeOption = CommonOptions.CreateUseCurrentRuntimeOption(CommandDefinitionStrings.CmdCurrentRuntimeOptionDescription);

        public readonly Option<bool> NoDependenciesOption = new Option<bool>("--no-dependencies")
        {
            Description = CommandDefinitionStrings.NoDependenciesOptionDescription,
            Arity = ArgumentArity.Zero
        }.ForwardAs("--property:BuildProjectReferences=false");

        public readonly Option<bool> NoAnsiOption = new("--no-ansi")
        {
            Description = CommandDefinitionStrings.CmdNoAnsiDescription,
            Arity = ArgumentArity.Zero
        };

        public readonly Option<bool> NoProgressOption = new("--no-progress")
        {
            Description = CommandDefinitionStrings.CmdNoProgressDescription,
            Arity = ArgumentArity.Zero
        };

        public readonly Option<bool> NoArtifactPostProcessingOption = new("--no-artifact-post-processing")
        {
            Description = CommandDefinitionStrings.CmdNoArtifactPostProcessingDescription,
            Arity = ArgumentArity.Zero
        };

        public readonly Option<OutputOptions> OutputOption = new("--output")
        {
            Description = CommandDefinitionStrings.CmdTestOutputDescription,
            Arity = ArgumentArity.ExactlyOne
        };

        public const string ListTestsOptionName = "--list-tests";

        public const string ListTestsFormatText = "text";

        public const string ListTestsFormatJson = "json";

        public readonly Option<string> ListTestsOption = new Option<string>(ListTestsOptionName)
        {
            Description = CommandDefinitionStrings.CmdListTestsDescription,
            HelpName = $"{ListTestsFormatText}|{ListTestsFormatJson}",
            Arity = ArgumentArity.ZeroOrOne
        }.AcceptOnlyFromAmong(ListTestsFormatText, ListTestsFormatJson);

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
            Description = CommandDefinitionStrings.CommandOptionDeviceDescriptionForTest,
            HelpName = CommandDefinitionStrings.CommandOptionDeviceHelpName
        };

        public readonly Option<bool> ListDevicesOption = new("--list-devices")
        {
            Description = CommandDefinitionStrings.CommandOptionListDevicesDescriptionForTest,
            Arity = ArgumentArity.Zero
        };

        public const string EnableAffectedTestsEnvironmentVariable = "DOTNET_CLI_ENABLE_AFFECTED_TESTS";

        public const string CollectTestMapOptionName = "--collect-test-map";

        public readonly Option<bool> CollectTestMapOption;

        public const string AffectedTestsOptionName = "--affected-tests";

        public readonly Option<bool> AffectedTestsOption;

        public bool AffectedTestsEnabled { get; }

        public readonly Option<string> ArtifactsPathOption = CommonOptions.CreateArtifactsPathOption();

        public const string BuildTargetName = "_MTPBuild";

        public readonly Option<string[]> MTPTargetOption = CommonOptions.CreateRequiredMSBuildTargetOption(BuildTargetName);

        public ICustomHelp? CustomHelpLayoutProvider { get; set; }

        public MicrosoftTestingPlatform()
            : base(CommandDefinitionStrings.DotnetTestCommandMTPDescription)
        {
            MinimumExpectedTestsOption.Validators.Add(ValidatePositiveInteger);
            MaximumFailedTestsOption.Validators.Add(ValidatePositiveInteger);

            AffectedTestsEnabled = EnvironmentVariableParser.ParseBool(
                Environment.GetEnvironmentVariable(EnableAffectedTestsEnvironmentVariable),
                defaultValue: false);

            CollectTestMapOption = new(CollectTestMapOptionName)
            {
                Description = CommandDefinitionStrings.CmdCollectTestMapDescription,
                Arity = ArgumentArity.Zero,
                Hidden = !AffectedTestsEnabled,
            };

            AffectedTestsOption = new(AffectedTestsOptionName)
            {
                Description = CommandDefinitionStrings.CmdAffectedTestsDescription,
                Arity = ArgumentArity.Zero,
                Hidden = !AffectedTestsEnabled,
            };

            Options.Add(ProjectOrSolutionOption);
            Options.Add(SolutionOption);
            Options.Add(TestModulesFilterOption);
            Options.Add(TestModulesRootDirectoryOption);
            Options.Add(ResultsDirectoryOption);
            Options.Add(ResultsDirectoryLayoutOption);
            Options.Add(ConfigFileOption);
            Options.Add(DiagnosticOutputDirectoryOption);
            Options.Add(MaxParallelTestModulesOption);
            Options.Add(MinimumExpectedTestsOption);
            Options.Add(MaximumFailedTestsOption);
            Options.Add(TimeoutOption);
            Options.Add(EnvOption);
            Options.Add(PropertiesOption);
            Options.Add(ConfigurationOption);
            Options.Add(FrameworkOption);
            TargetPlatformOptions.AddTo(Options);
            Options.Add(VerbosityOption);
            Options.Add(NoRestoreOption);
            Options.Add(NoBuildOption);
            NoLogoOption.Aliases.Add("--no-banner");
            Options.Add(NoLogoOption);
            Options.Add(NoDependenciesOption);
            Options.Add(ArtifactsPathOption);
            Options.Add(UseCurrentRuntimeOption);
            Options.Add(NoAnsiOption);
            Options.Add(NoProgressOption);
            Options.Add(NoArtifactPostProcessingOption);
            Options.Add(OutputOption);
            Options.Add(ListTestsOption);
            Options.Add(NoLaunchProfileOption);
            Options.Add(NoLaunchProfileArgumentsOption);
            Options.Add(DeviceOption);
            Options.Add(ListDevicesOption);
            Options.Add(CollectTestMapOption);
            Options.Add(AffectedTestsOption);
            Options.Add(MTPTargetOption);

            Validators.Add(commandResult =>
            {
                bool collectTestMap = commandResult.HasOption(CollectTestMapOption);
                bool affectedTests = commandResult.HasOption(AffectedTestsOption);
                if (!AffectedTestsEnabled && (collectTestMap || affectedTests))
                {
                    commandResult.AddError(string.Format(
                        CommandDefinitionStrings.CmdAffectedTestsFeatureDisabled,
                        EnableAffectedTestsEnvironmentVariable));
                }
                else if (collectTestMap && affectedTests)
                {
                    commandResult.AddError(CommandDefinitionStrings.CmdAffectedTestsOptionsMutuallyExclusive);
                }
                else if (collectTestMap && commandResult.HasOption(MaxParallelTestModulesOption))
                {
                    commandResult.AddError(CommandDefinitionStrings.CmdCollectTestMapCannotRunModulesInParallel);
                }
                else if (collectTestMap && commandResult.HasOption(MinimumExpectedTestsOption))
                {
                    commandResult.AddError(CommandDefinitionStrings.CmdCollectTestMapCannotRequireMinimumTests);
                }
            });
        }

        public IEnumerable<Action<HelpContext>> CustomHelpLayout()
            => CustomHelpLayoutProvider?.CustomHelpLayout() ?? [];

        private static void ValidatePositiveInteger(OptionResult optionResult)
        {
            if (optionResult.Tokens.Count == 1 &&
                int.TryParse(optionResult.Tokens[0].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) &&
                value <= 0)
            {
                optionResult.AddError(CommandDefinitionStrings.CmdTestPositiveIntegerRequired);
            }
        }

        private static TimeSpan? ParseTimeout(ArgumentResult argumentResult)
        {
            if (argumentResult.Tokens.Count != 1)
            {
                return null;
            }

            string value = argumentResult.Tokens[0].Value;
            Match match = TimeoutPattern().Match(value);
            if (!match.Success ||
                !double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
            {
                argumentResult.AddError(CommandDefinitionStrings.CmdTestInvalidTimeout);
                return null;
            }

            string suffix = match.Groups["suffix"].Value;
            TimeSpan timeout;
            try
            {
                timeout = suffix.StartsWith("ms", StringComparison.OrdinalIgnoreCase) ||
                          suffix.StartsWith("mil", StringComparison.OrdinalIgnoreCase)
                    ? TimeSpan.FromMilliseconds(number)
                    : suffix.StartsWith("s", StringComparison.OrdinalIgnoreCase)
                        ? TimeSpan.FromSeconds(number)
                        : suffix.StartsWith("m", StringComparison.OrdinalIgnoreCase)
                            ? TimeSpan.FromMinutes(number)
                            : suffix.StartsWith("h", StringComparison.OrdinalIgnoreCase)
                                ? TimeSpan.FromHours(number)
                                : TimeSpan.FromDays(number);
            }
            catch (Exception ex) when (ex is OverflowException or ArgumentException)
            {
                argumentResult.AddError(CommandDefinitionStrings.CmdTestInvalidTimeout);
                return null;
            }

            if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > MaxSupportedTimeoutMilliseconds)
            {
                argumentResult.AddError(CommandDefinitionStrings.CmdTestInvalidTimeout);
                return null;
            }

            return timeout;
        }

        [GeneratedRegex(
            @"^(?<value>\d+(?:\.\d+)?)(?:\s*(?<suffix>ms|mils?|milliseconds?|s|secs?|seconds?|m|mins?|minutes?|h|hours?|d|days?))$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex TimeoutPattern();
    }
}
