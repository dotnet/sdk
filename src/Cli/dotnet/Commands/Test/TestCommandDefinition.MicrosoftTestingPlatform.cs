// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Cli.Help;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal abstract partial class TestCommandDefinition
{
    public sealed class MicrosoftTestingPlatform : TestCommandDefinition, ICustomHelp
    {
        public readonly Option<string> ProjectOrSolutionOption = new("--project")
        {
            Description = CliCommandStrings.CmdProjectOrSolutionDescriptionFormat,
            HelpName = CliCommandStrings.CmdProjectOrSolutionPathName,
            Arity = ArgumentArity.ExactlyOne
        };

        public readonly Option<string> SolutionOption = new("--solution")
        {
            Description = CliCommandStrings.CmdSolutionDescription,
            HelpName = CliCommandStrings.CmdSolutionPathName,
            Arity = ArgumentArity.ExactlyOne
        };

        public readonly Option<string> TestModulesFilterOption = new("--test-modules")
        {
            Description = CliCommandStrings.CmdTestModulesDescription,
            HelpName = CliCommandStrings.CmdExpressionName
        };

        public readonly Option<string> TestModulesRootDirectoryOption = new("--root-directory")
        {
            Description = CliCommandStrings.CmdTestModulesRootDirectoryDescription,
            HelpName = CliCommandStrings.CmdRootPathName,
        };

        public const string ResultsDirectoryOptionName = "--results-directory";

        public readonly Option<string> ResultsDirectoryOption = new(ResultsDirectoryOptionName)
        {
            Description = CliCommandStrings.CmdResultsDirectoryDescription,
            HelpName = CliCommandStrings.CmdPathToResultsDirectory,
            Arity = ArgumentArity.ExactlyOne
        };

        public const string ConfigFileOptionName = "--config-file";

        public readonly Option<string> ConfigFileOption = new(ConfigFileOptionName)
        {
            Description = CliCommandStrings.CmdConfigFileDescription,
            HelpName = CliCommandStrings.CmdConfigFilePath,
            Arity = ArgumentArity.ExactlyOne
        };

        public const string DiagnosticOutputDirectoryOptionName = "--diagnostic-output-directory";

        public readonly Option<string> DiagnosticOutputDirectoryOption = new(DiagnosticOutputDirectoryOptionName)
        {
            Description = CliCommandStrings.CmdDiagnosticOutputDirectoryDescription,
            HelpName = CliCommandStrings.CmdDiagnosticOutputDirectoryPath,
            Arity = ArgumentArity.ExactlyOne
        };

        public readonly Option<int> MaxParallelTestModulesOption = new("--max-parallel-test-modules")
        {
            Description = CliCommandStrings.CmdMaxParallelTestModulesDescription,
            HelpName = CliCommandStrings.CmdNumberName
        };

        public readonly Option<int> MinimumExpectedTestsOption = new("--minimum-expected-tests")
        {
            Description = CliCommandStrings.CmdMinimumExpectedTestsDescription,
            HelpName = CliCommandStrings.CmdNumberName
        };

        public readonly Option<IReadOnlyDictionary<string, string>> EnvOption = CommonOptions.CreateEnvOption();

        public readonly Option<ReadOnlyDictionary<string, string>?> PropertiesOption = CommonOptions.CreatePropertyOption();

        public readonly Option<bool> NoRestoreOption = CommonOptions.CreateNoRestoreOption();

        public readonly Option<bool> NoBuildOption = new("--no-build")
        {
            Description = CliCommandStrings.CmdNoBuildDescription
        };

        public readonly Option<bool> NoAnsiOption = new("--no-ansi")
        {
            Description = CliCommandStrings.CmdNoAnsiDescription,
            Arity = ArgumentArity.Zero
        };

        public readonly Option<bool> NoProgressOption = new("--no-progress")
        {
            Description = CliCommandStrings.CmdNoProgressDescription,
            Arity = ArgumentArity.Zero
        };

        public readonly Option<OutputOptions> OutputOption = new("--output")
        {
            Description = CliCommandStrings.CmdTestOutputDescription,
            Arity = ArgumentArity.ExactlyOne
        };

        public const string ListTestsOptionName = "--list-tests";

        public readonly Option<string> ListTestsOption = new(ListTestsOptionName)
        {
            Description = CliCommandStrings.CmdListTestsDescription,
            Arity = ArgumentArity.Zero
        };

        public readonly Option<bool> NoLaunchProfileOption = new("--no-launch-profile")
        {
            Description = CliCommandStrings.CommandOptionNoLaunchProfileDescription,
            Arity = ArgumentArity.Zero
        };

        public readonly Option<bool> NoLaunchProfileArgumentsOption = new("--no-launch-profile-arguments")
        {
            Description = CliCommandStrings.CommandOptionNoLaunchProfileArgumentsDescription
        };

        public readonly Option<string[]> MTPTargetOption = CommonOptions.CreateRequiredMSBuildTargetOption(CliConstants.MTPTarget);

        public ICustomHelp? CustomHelpLayoutProvider { get; set; }

        public MicrosoftTestingPlatform()
            : base(CliCommandStrings.DotnetTestCommandMTPDescription)
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
