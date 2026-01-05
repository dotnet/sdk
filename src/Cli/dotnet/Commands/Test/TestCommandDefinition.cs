// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Cli.Help;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal abstract class TestCommandDefinition : Command
{
    private const string Link = "https://aka.ms/dotnet-test";

    public readonly Option<string> FrameworkOption = CommonOptions.CreateFrameworkOption(CliCommandStrings.TestFrameworkOptionDescription);

    public readonly Option<string?> ConfigurationOption = CommonOptions.CreateConfigurationOption(CliCommandStrings.TestConfigurationOptionDescription);

    public readonly Option<Utils.VerbosityOptions?> VerbosityOption = CommonOptions.CreateVerbosityOption();

    public TestCommandDefinition(string description)
        : base("test", description)
    {
        this.DocsLink = Link;
        TreatUnmatchedTokensAsErrors = false;
    }

    public static TestCommandDefinition Create()
    {
        string? globalJsonPath = GetGlobalJsonPath(Environment.CurrentDirectory);
        if (!File.Exists(globalJsonPath))
        {
            return new VSTest();
        }

        string jsonText = File.ReadAllText(globalJsonPath);

        // This code path is hit exactly once during the whole life of the dotnet process.
        // So, no concern about caching JsonSerializerOptions.
        var globalJson = JsonSerializer.Deserialize<GlobalJsonModel>(jsonText, new JsonSerializerOptions()
        {
            AllowDuplicateProperties = false,
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Skip,
        });

        var name = globalJson?.Test?.RunnerName;

        if (name is null || name.Equals(CliConstants.VSTest, StringComparison.OrdinalIgnoreCase))
        {
            return new VSTest();
        }

        if (name.Equals(CliConstants.MicrosoftTestingPlatform, StringComparison.OrdinalIgnoreCase))
        {
            return new MicrosoftTestingPlatform();
        }

        throw new InvalidOperationException(string.Format(CliCommandStrings.CmdUnsupportedTestRunnerDescription, name));
    }

    private static string? GetGlobalJsonPath(string? startDir)
    {
        string? directory = startDir;
        while (directory != null)
        {
            string globalJsonPath = Path.Combine(directory, "global.json");
            if (File.Exists(globalJsonPath))
            {
                return globalJsonPath;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }

    private sealed class GlobalJsonModel
    {
        [JsonPropertyName("test")]
        public GlobalJsonTestNode Test { get; set; } = null!;
    }

    private sealed class GlobalJsonTestNode
    {
        [JsonPropertyName("runner")]
        public string RunnerName { get; set; } = null!;
    }

    public sealed class MicrosoftTestingPlatform : TestCommandDefinition, ICustomHelp
    {
        public readonly Option<string> ProjectOrSolutionOption = MicrosoftTestingPlatformOptions.ProjectOrSolutionOption;
        public readonly Option<string> SolutionOption = MicrosoftTestingPlatformOptions.SolutionOption;
        public readonly Option<string> TestModulesFilterOption = MicrosoftTestingPlatformOptions.TestModulesFilterOption;
        public readonly Option<string> TestModulesRootDirectoryOption = MicrosoftTestingPlatformOptions.TestModulesRootDirectoryOption;
        public readonly Option<string> ResultsDirectoryOption = MicrosoftTestingPlatformOptions.ResultsDirectoryOption;
        public readonly Option<string> ConfigFileOption = MicrosoftTestingPlatformOptions.ConfigFileOption;
        public readonly Option<string> DiagnosticOutputDirectoryOption = MicrosoftTestingPlatformOptions.DiagnosticOutputDirectoryOption;
        public readonly Option<int> MaxParallelTestModulesOption = MicrosoftTestingPlatformOptions.MaxParallelTestModulesOption;
        public readonly Option<int> MinimumExpectedTestsOption = MicrosoftTestingPlatformOptions.MinimumExpectedTestsOption;
        public readonly Option<string> ArchitectureOption = CommonOptions.ArchitectureOption;
        public readonly Option<IReadOnlyDictionary<string, string>> EnvOption = CommonOptions.EnvOption;
        public readonly Option<ReadOnlyDictionary<string, string>?> PropertiesOption = CommonOptions.PropertiesOption;
        public readonly Option<string> OperatingSystemOption = CommonOptions.OperatingSystemOption;
        public readonly Option<string> RuntimeOption = CommonOptions.CreateRuntimeOption(CliCommandStrings.TestRuntimeOptionDescription);
        public readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;
        public readonly Option<bool> NoBuildOption = MicrosoftTestingPlatformOptions.NoBuildOption;
        public readonly Option<bool> NoAnsiOption = MicrosoftTestingPlatformOptions.NoAnsiOption;
        public readonly Option<bool> NoProgressOption = MicrosoftTestingPlatformOptions.NoProgressOption;
        public readonly Option<OutputOptions> OutputOption = MicrosoftTestingPlatformOptions.OutputOption;
        public readonly Option<string> ListTestsOption = MicrosoftTestingPlatformOptions.ListTestsOption;
        public readonly Option<bool> NoLaunchProfileOption = MicrosoftTestingPlatformOptions.NoLaunchProfileOption;
        public readonly Option<bool> NoLaunchProfileArgumentsOption = MicrosoftTestingPlatformOptions.NoLaunchProfileArgumentsOption;
        public readonly Option<string[]> MTPTargetOption = MicrosoftTestingPlatformOptions.MTPTargetOption;

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
            Options.Add(ArchitectureOption);
            Options.Add(EnvOption);
            Options.Add(PropertiesOption);
            Options.Add(ConfigurationOption);
            Options.Add(FrameworkOption);
            Options.Add(OperatingSystemOption);
            Options.Add(RuntimeOption);
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

    public sealed class VSTest : TestCommandDefinition
    {
        public readonly Option<string> SettingsOption = VSTestOptions.SettingsOption;
        public readonly Option<bool> ListTestsOption = VSTestOptions.ListTestsOption;
        public readonly Option<IReadOnlyDictionary<string, string>> TestEnvOption = CommonOptions.TestEnvOption;
        public readonly Option<string> FilterOption = VSTestOptions.FilterOption;
        public readonly Option<IEnumerable<string>> AdapterOption = VSTestOptions.AdapterOption;
        public readonly Option<IEnumerable<string>> LoggerOption = VSTestOptions.LoggerOption;
        public readonly Option<string> OutputOption = VSTestOptions.OutputOption;
        public readonly Option<string> ArtifactsPathOption = CommonOptions.CreateArtifactsPathOption();
        public readonly Option<string> DiagOption = VSTestOptions.DiagOption;
        public readonly Option<bool> NoBuildOption = VSTestOptions.NoBuildOption;
        public readonly Option<string> ResultsOption = VSTestOptions.ResultsOption;
        public readonly Option<IEnumerable<string>> CollectOption = VSTestOptions.CollectOption;
        public readonly Option<bool> BlameOption = VSTestOptions.BlameOption;
        public readonly Option<bool> BlameCrashOption = VSTestOptions.BlameCrashOption;
        public readonly Option<string> BlameCrashDumpOption = VSTestOptions.BlameCrashDumpOption;
        public readonly Option<bool> BlameCrashAlwaysOption = VSTestOptions.BlameCrashAlwaysOption;
        public readonly Option<bool> BlameHangOption = VSTestOptions.BlameHangOption;
        public readonly Option<string> BlameHangDumpOption = VSTestOptions.BlameHangDumpOption;
        public readonly Option<string> BlameHangTimeoutOption = VSTestOptions.BlameHangTimeoutOption;
        public readonly Option<bool> NoLogoOption = VSTestOptions.NoLogoOption;
        public readonly Option<string> RuntimeOption = CommonOptions.CreateRuntimeOption(CliCommandStrings.TestRuntimeOptionDescription);
        public readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;
        public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveMsBuildForwardOption();
        public readonly Option<string> ArchitectureOption = CommonOptions.ArchitectureOption;
        public readonly Option<string> OperatingSystemOption = CommonOptions.OperatingSystemOption;
        public readonly Option<ReadOnlyDictionary<string, string>?> PropertiesOption = CommonOptions.PropertiesOption;
        public readonly Option<bool> DisableBuildServersOption = CommonOptions.CreateDisableBuildServersOption();
        public readonly Option<string[]> VsTestTargetOption = VSTestOptions.VsTestTargetOption;

        public VSTest()
            : base(CliCommandStrings.DotnetTestCommandVSTestDescription)
        {
            // We are on purpose not capturing the solution, project or directory here. We want to pass it to the
            // MSBuild command so we are letting it flow.

            Options.Add(SettingsOption);
            Options.Add(ListTestsOption);
            Options.Add(TestEnvOption);
            Options.Add(FilterOption);
            Options.Add(AdapterOption);
            Options.Add(LoggerOption);
            Options.Add(OutputOption);
            Options.Add(ArtifactsPathOption);
            Options.Add(DiagOption);
            Options.Add(NoBuildOption);
            Options.Add(ResultsOption);
            Options.Add(CollectOption);
            Options.Add(BlameOption);
            Options.Add(BlameCrashOption);
            Options.Add(BlameCrashDumpOption);
            Options.Add(BlameCrashAlwaysOption);
            Options.Add(BlameHangOption);
            Options.Add(BlameHangDumpOption);
            Options.Add(BlameHangTimeoutOption);
            Options.Add(NoLogoOption);
            Options.Add(ConfigurationOption);
            Options.Add(FrameworkOption);
            Options.Add(RuntimeOption);
            Options.Add(NoRestoreOption);
            Options.Add(InteractiveOption);
            Options.Add(VerbosityOption);
            Options.Add(ArchitectureOption);
            Options.Add(OperatingSystemOption);
            Options.Add(PropertiesOption);
            Options.Add(DisableBuildServersOption);
            Options.Add(VsTestTargetOption);
        }
    }
}
