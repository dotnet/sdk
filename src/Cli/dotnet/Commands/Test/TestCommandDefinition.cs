// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotNet.Cli.CommandLine;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class TestCommandDefinition
{
    public enum TestRunner
    {
        VSTest,
        MicrosoftTestingPlatform
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

    public const string Name = "test";
    public static readonly string DocsLink = "https://aka.ms/dotnet-test";

    public static readonly Option<string> FrameworkOption = CommonOptions.FrameworkOption(CliCommandStrings.TestFrameworkOptionDescription);

    public static readonly Option<string?> ConfigurationOption = CommonOptions.ConfigurationOption(CliCommandStrings.TestConfigurationOptionDescription);

    public static readonly Option<Utils.VerbosityOptions?> VerbosityOption = CommonOptions.VerbosityOption();

    public static TestRunner GetTestRunner()
    {
        string? globalJsonPath = GetGlobalJsonPath(Environment.CurrentDirectory);
        if (!File.Exists(globalJsonPath))
        {
            return TestRunner.VSTest;
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
            return TestRunner.VSTest;
        }

        if (name.Equals(CliConstants.MicrosoftTestingPlatform, StringComparison.OrdinalIgnoreCase))
        {
            return TestRunner.MicrosoftTestingPlatform;
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

    public static void ConfigureMicrosoftTestingPlatformCommand(Command command)
    {
        command.Description = CliCommandStrings.DotnetTestCommandMTPDescription;
        command.Options.Add(MicrosoftTestingPlatformOptions.ProjectOrSolutionOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.SolutionOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.TestModulesFilterOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.TestModulesRootDirectoryOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.ResultsDirectoryOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.ConfigFileOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.DiagnosticOutputDirectoryOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.MaxParallelTestModulesOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.MinimumExpectedTestsOption);
        command.Options.Add(CommonOptions.ArchitectureOption);
        command.Options.Add(CommonOptions.EnvOption);
        command.Options.Add(CommonOptions.PropertiesOption);
        command.Options.Add(ConfigurationOption);
        command.Options.Add(FrameworkOption);
        command.Options.Add(CommonOptions.OperatingSystemOption);
        command.Options.Add(CommonOptions.RuntimeOption(CliCommandStrings.TestRuntimeOptionDescription));
        command.Options.Add(VerbosityOption);
        command.Options.Add(CommonOptions.NoRestoreOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.NoBuildOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.NoAnsiOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.NoProgressOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.OutputOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.ListTestsOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.NoLaunchProfileOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.NoLaunchProfileArgumentsOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.MTPTargetOption);
    }

    public static void ConfigureVSTestCommand(Command command)
    {
        command.Description = CliCommandStrings.DotnetTestCommandVSTestDescription;
        command.TreatUnmatchedTokensAsErrors = false;
        command.DocsLink = DocsLink;

        // We are on purpose not capturing the solution, project or directory here. We want to pass it to the
        // MSBuild command so we are letting it flow.

        command.Options.Add(VSTestOptions.SettingsOption);
        command.Options.Add(VSTestOptions.ListTestsOption);
        command.Options.Add(CommonOptions.TestEnvOption);
        command.Options.Add(VSTestOptions.FilterOption);
        command.Options.Add(VSTestOptions.AdapterOption);
        command.Options.Add(VSTestOptions.LoggerOption);
        command.Options.Add(VSTestOptions.OutputOption);
        command.Options.Add(CommonOptions.ArtifactsPathOption);
        command.Options.Add(VSTestOptions.DiagOption);
        command.Options.Add(VSTestOptions.NoBuildOption);
        command.Options.Add(VSTestOptions.ResultsOption);
        command.Options.Add(VSTestOptions.CollectOption);
        command.Options.Add(VSTestOptions.BlameOption);
        command.Options.Add(VSTestOptions.BlameCrashOption);
        command.Options.Add(VSTestOptions.BlameCrashDumpOption);
        command.Options.Add(VSTestOptions.BlameCrashAlwaysOption);
        command.Options.Add(VSTestOptions.BlameHangOption);
        command.Options.Add(VSTestOptions.BlameHangDumpOption);
        command.Options.Add(VSTestOptions.BlameHangTimeoutOption);
        command.Options.Add(VSTestOptions.NoLogoOption);
        command.Options.Add(ConfigurationOption);
        command.Options.Add(FrameworkOption);
        command.Options.Add(CommonOptions.RuntimeOption(CliCommandStrings.TestRuntimeOptionDescription));
        command.Options.Add(CommonOptions.NoRestoreOption);
        command.Options.Add(CommonOptions.InteractiveMsBuildForwardOption);
        command.Options.Add(VerbosityOption);
        command.Options.Add(CommonOptions.ArchitectureOption);
        command.Options.Add(CommonOptions.OperatingSystemOption);
        command.Options.Add(CommonOptions.PropertiesOption);
        command.Options.Add(CommonOptions.DisableBuildServersOption);
        command.Options.Add(VSTestOptions.VsTestTargetOption);
    }
}
