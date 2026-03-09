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
    {
        string? globalJsonPath = GetGlobalJsonPath(Environment.CurrentDirectory);
        if (!File.Exists(globalJsonPath))
        {
            return new VSTest();
        }

        string jsonText = File.ReadAllText(globalJsonPath);

        var globalJson = JsonSerializer.Deserialize(jsonText, GlobalJsonSerializerContext.Default.GlobalJsonModel);

        var name = globalJson?.Test?.RunnerName;

        if (name is null || name.Equals(VSTestRunnerName, StringComparison.OrdinalIgnoreCase))
        {
            return new VSTest();
        }

        if (name.Equals(MicrosoftTestingPlatformRunnerName, StringComparison.OrdinalIgnoreCase))
        {
            return new MicrosoftTestingPlatform();
        }

        throw new InvalidOperationException(string.Format(CommandDefinitionStrings.CmdUnsupportedTestRunnerDescription, name));
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
