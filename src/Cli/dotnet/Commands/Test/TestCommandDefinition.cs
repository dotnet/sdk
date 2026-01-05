// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

    public static readonly Option<string> FrameworkOption = CommonOptions.CreateFrameworkOption(CliCommandStrings.TestFrameworkOptionDescription);

    public static readonly Option<string?> ConfigurationOption = CommonOptions.CreateConfigurationOption(CliCommandStrings.TestConfigurationOptionDescription);

    public static readonly Option<Utils.VerbosityOptions?> VerbosityOption = CommonOptions.CreateVerbosityOption();

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
        public ICustomHelp? CustomHelpLayoutProvider { get; set; }

        public MicrosoftTestingPlatform()
            : base(CliCommandStrings.DotnetTestCommandMTPDescription)
        {
            Options.Add(MicrosoftTestingPlatformOptions.ProjectOrSolutionOption);
            Options.Add(MicrosoftTestingPlatformOptions.SolutionOption);
            Options.Add(MicrosoftTestingPlatformOptions.TestModulesFilterOption);
            Options.Add(MicrosoftTestingPlatformOptions.TestModulesRootDirectoryOption);
            Options.Add(MicrosoftTestingPlatformOptions.ResultsDirectoryOption);
            Options.Add(MicrosoftTestingPlatformOptions.ConfigFileOption);
            Options.Add(MicrosoftTestingPlatformOptions.DiagnosticOutputDirectoryOption);
            Options.Add(MicrosoftTestingPlatformOptions.MaxParallelTestModulesOption);
            Options.Add(MicrosoftTestingPlatformOptions.MinimumExpectedTestsOption);
            Options.Add(CommonOptions.ArchitectureOption);
            Options.Add(CommonOptions.EnvOption);
            Options.Add(CommonOptions.PropertiesOption);
            Options.Add(ConfigurationOption);
            Options.Add(FrameworkOption);
            Options.Add(CommonOptions.OperatingSystemOption);
            Options.Add(CommonOptions.CreateRuntimeOption(CliCommandStrings.TestRuntimeOptionDescription));
            Options.Add(VerbosityOption);
            Options.Add(CommonOptions.NoRestoreOption);
            Options.Add(MicrosoftTestingPlatformOptions.NoBuildOption);
            Options.Add(MicrosoftTestingPlatformOptions.NoAnsiOption);
            Options.Add(MicrosoftTestingPlatformOptions.NoProgressOption);
            Options.Add(MicrosoftTestingPlatformOptions.OutputOption);
            Options.Add(MicrosoftTestingPlatformOptions.ListTestsOption);
            Options.Add(MicrosoftTestingPlatformOptions.NoLaunchProfileOption);
            Options.Add(MicrosoftTestingPlatformOptions.NoLaunchProfileArgumentsOption);
            Options.Add(MicrosoftTestingPlatformOptions.MTPTargetOption);
        }

        public IEnumerable<Action<HelpContext>> CustomHelpLayout()
            => CustomHelpLayoutProvider?.CustomHelpLayout() ?? [];
    }

    public sealed class VSTest : TestCommandDefinition
    {
        public VSTest()
            : base(CliCommandStrings.DotnetTestCommandVSTestDescription)
        {
            // We are on purpose not capturing the solution, project or directory here. We want to pass it to the
            // MSBuild command so we are letting it flow.

            Options.Add(VSTestOptions.SettingsOption);
            Options.Add(VSTestOptions.ListTestsOption);
            Options.Add(CommonOptions.TestEnvOption);
            Options.Add(VSTestOptions.FilterOption);
            Options.Add(VSTestOptions.AdapterOption);
            Options.Add(VSTestOptions.LoggerOption);
            Options.Add(VSTestOptions.OutputOption);
            Options.Add(CommonOptions.CreateArtifactsPathOption());
            Options.Add(VSTestOptions.DiagOption);
            Options.Add(VSTestOptions.NoBuildOption);
            Options.Add(VSTestOptions.ResultsOption);
            Options.Add(VSTestOptions.CollectOption);
            Options.Add(VSTestOptions.BlameOption);
            Options.Add(VSTestOptions.BlameCrashOption);
            Options.Add(VSTestOptions.BlameCrashDumpOption);
            Options.Add(VSTestOptions.BlameCrashAlwaysOption);
            Options.Add(VSTestOptions.BlameHangOption);
            Options.Add(VSTestOptions.BlameHangDumpOption);
            Options.Add(VSTestOptions.BlameHangTimeoutOption);
            Options.Add(VSTestOptions.NoLogoOption);
            Options.Add(ConfigurationOption);
            Options.Add(FrameworkOption);
            Options.Add(CommonOptions.CreateRuntimeOption(CliCommandStrings.TestRuntimeOptionDescription));
            Options.Add(CommonOptions.NoRestoreOption);
            Options.Add(CommonOptions.CreateInteractiveMsBuildForwardOption());
            Options.Add(VerbosityOption);
            Options.Add(CommonOptions.ArchitectureOption);
            Options.Add(CommonOptions.OperatingSystemOption);
            Options.Add(CommonOptions.PropertiesOption);
            Options.Add(CommonOptions.CreateDisableBuildServersOption());
            Options.Add(VSTestOptions.VsTestTargetOption);
        }
    }
}
