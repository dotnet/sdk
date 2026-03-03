// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.CommandLine;
using Microsoft.DotNet.Cli.Help;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal abstract partial class TestCommandDefinition
{
    public sealed class MicrosoftTestingPlatform : TestCommandDefinition, ICustomHelp
    {
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

        public readonly Option<IReadOnlyDictionary<string, string>> EnvOption = CommonOptions.CreateEnvOption();

        public readonly Option<ReadOnlyDictionary<string, string>?> PropertiesOption = CommonOptions.CreatePropertyOption();

        public readonly Option<bool> NoRestoreOption = CommonOptions.CreateNoRestoreOption();

        public readonly Option<bool> NoBuildOption = new("--no-build")
        {
            Description = CommandDefinitionStrings.CmdNoBuildDescription
        };

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

        public readonly Option<OutputOptions> OutputOption = new("--output")
        {
            Description = CommandDefinitionStrings.CmdTestOutputDescription,
            Arity = ArgumentArity.ExactlyOne
        };

        public const string ListTestsOptionName = "--list-tests";

        public readonly Option<string> ListTestsOption = new(ListTestsOptionName)
        {
            Description = CommandDefinitionStrings.CmdListTestsDescription,
            Arity = ArgumentArity.Zero
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

        public const string BuildTargetName = "_MTPBuild";

        public readonly Option<string[]> MTPTargetOption = CommonOptions.CreateRequiredMSBuildTargetOption(BuildTargetName);

        public ICustomHelp? CustomHelpLayoutProvider { get; set; }

        public MicrosoftTestingPlatform()
            : base(CommandDefinitionStrings.DotnetTestCommandMTPDescription)
        {
            Options.Add(ProjectOrSolutionOption);
            Options.Add(SolutionOption);
            Options.Add(TestModulesFilterOption);
            Options.Add(TestModulesRootDirectoryOption);
            Options.Add(ResultsDirectoryOption);
            Options.Add(ConfigFileOption);
            Options.Add(DiagnosticOutputDirectoryOption);
            Options.Add(MaxParallelTestModulesOption);
            Options.Add(MinimumExpectedTestsOption);
            Options.Add(EnvOption);
            Options.Add(PropertiesOption);
            Options.Add(ConfigurationOption);
            Options.Add(FrameworkOption);
            TargetPlatformOptions.AddTo(Options);
            Options.Add(VerbosityOption);
            Options.Add(NoRestoreOption);
            Options.Add(NoBuildOption);
            Options.Add(NoAnsiOption);
            Options.Add(NoProgressOption);
            Options.Add(OutputOption);
            Options.Add(ListTestsOption);
            Options.Add(NoLaunchProfileOption);
            Options.Add(NoLaunchProfileArgumentsOption);
            Options.Add(MTPTargetOption);
        }

        public IEnumerable<Action<HelpContext>> CustomHelpLayout()
            => CustomHelpLayoutProvider?.CustomHelpLayout() ?? [];
    }
}
