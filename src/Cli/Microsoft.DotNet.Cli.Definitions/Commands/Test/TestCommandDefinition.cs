// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotNet.Cli.CommandLine;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal abstract partial class TestCommandDefinition : Command
{
    private const string Link = "https://aka.ms/dotnet-test";
    private const string VSTestRunnerName = "VSTest";
    private const string MicrosoftTestingPlatformRunnerName = "Microsoft.Testing.Platform";
    private const string TestRunnerEnvironmentVariableName = "DOTNET_TEST_RUNNER";

    public readonly TargetPlatformOptions TargetPlatformOptions = new(CommandDefinitionStrings.TestRuntimeOptionDescription);

    public readonly Option<string> FrameworkOption = CommonOptions.CreateFrameworkOption(CommandDefinitionStrings.TestFrameworkOptionDescription);

    public readonly Option<string?> ConfigurationOption = CommonOptions.CreateConfigurationOption(CommandDefinitionStrings.TestConfigurationOptionDescription);

    public readonly Option<Utils.VerbosityOptions?> VerbosityOption = CommonOptions.CreateVerbosityOption();

    public TestCommandDefinition(string description)
        : base("test", description)
    {
        this.DocsLink = Link;
        TreatUnmatchedTokensAsErrors = false;
    }

    public static TestCommandDefinition Create()
        => Create(Environment.CurrentDirectory, Environment.GetEnvironmentVariable(TestRunnerEnvironmentVariableName));

    internal static TestCommandDefinition Create(string startDirectory)
        => Create(startDirectory, Environment.GetEnvironmentVariable(TestRunnerEnvironmentVariableName));

    internal static TestCommandDefinition Create(string startDirectory, string? testRunnerEnvironmentValue)
    {
        // The DOTNET_TEST_RUNNER environment variable lets users pick a runner without editing
        // (or carrying multiple) global.json files. When set to a recognized value it takes
        // precedence over the global.json `test.runner` setting.
        //
        // The runner name is resolved during CLI parser construction, which runs for *every*
        // dotnet invocation (e.g. `dotnet --version`, `dotnet build`), so an unrecognized env
        // var value must not crash unrelated commands. Unknown/whitespace values silently fall
        // back to the global.json lookup; an unknown value in global.json itself remains a
        // hard error because that file is explicit, in-repo configuration the user can fix.
        string? trimmedEnvValue = testRunnerEnvironmentValue?.Trim();
        if (!string.IsNullOrEmpty(trimmedEnvValue) && TryResolveRunner(trimmedEnvValue) is { } runnerFromEnv)
        {
            return runnerFromEnv;
        }

        string? globalJsonRunnerName = TryGetRunnerNameFromGlobalJson(startDirectory);
        if (globalJsonRunnerName is null)
        {
            return new VSTest();
        }

        return TryResolveRunner(globalJsonRunnerName)
            ?? throw new InvalidOperationException(string.Format(CommandDefinitionStrings.CmdUnsupportedTestRunnerDescription, globalJsonRunnerName));
    }

    private static TestCommandDefinition? TryResolveRunner(string name)
    {
        if (name.Equals(VSTestRunnerName, StringComparison.OrdinalIgnoreCase))
        {
            return new VSTest();
        }

        if (name.Equals(MicrosoftTestingPlatformRunnerName, StringComparison.OrdinalIgnoreCase))
        {
            return new MicrosoftTestingPlatform();
        }

        return null;
    }

    private static string? TryGetRunnerNameFromGlobalJson(string startDirectory)
    {
        string? globalJsonPath = GetGlobalJsonPath(startDirectory);
        if (globalJsonPath is null)
        {
            return null;
        }

        try
        {
            string jsonText = File.ReadAllText(globalJsonPath);
            GlobalJsonModel? globalJson = JsonSerializer.Deserialize(jsonText, GlobalJsonSerializerContext.Default.GlobalJsonModel);
            return globalJson?.Test?.RunnerName;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // The global.json file is unreadable or malformed (for example, empty or mid-edit). This
            // method is invoked very early during CLI parser construction, so throwing here would
            // bring down ALL commands (including `dotnet --version`). Fall back to the default
            // runner; if the user actually runs `dotnet test`, the test command itself will surface
            // a clearer error when it tries to load the configuration.
            return null;
        }
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

    [JsonSourceGenerationOptions(ReadCommentHandling = JsonCommentHandling.Skip)]
    [JsonSerializable(typeof(GlobalJsonModel))]
    private partial class GlobalJsonSerializerContext : JsonSerializerContext;
}
