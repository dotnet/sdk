// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Telemetry;

internal class LLMEnvironmentDetectorForTelemetry : ILLMEnvironmentDetector
{
    private static readonly EnvironmentDetectionRuleWithResult<string>[] _detectionRules = [
        // Claude Code
        new EnvironmentDetectionRuleWithResult<string>("claude", new AnyPresentEnvironmentRule("CLAUDECODE")),
        // Cursor AI
        new EnvironmentDetectionRuleWithResult<string>("cursor", new AnyPresentEnvironmentRule("CURSOR_EDITOR")),
        // Gemini
        new EnvironmentDetectionRuleWithResult<string>("gemini", new BooleanEnvironmentRule("GEMINI_CLI")),
        // GitHub Copilot CLI (including the legacy gh extension)
        new EnvironmentDetectionRuleWithResult<string>("copilot-cli", new AnyPresentEnvironmentRule(
            "GITHUB_COPILOT_CLI_MODE", "GH_COPILOT_WORKING_DIRECTORY", "COPILOT_CLI", "COPILOT_MODEL", "COPILOT_ALLOW_ALL", "COPILOT_GITHUB_TOKEN")),
        // GitHub Copilot agent mode in VS Code
        new EnvironmentDetectionRuleWithResult<string>("copilot-vscode", new AnyMatchEnvironmentRule(
            new EnvironmentVariableValueRule("AI_AGENT", "github_copilot_vscode_agent"),
            new AnyPresentEnvironmentRule("COPILOT_AGENT"))),
        // GitHub Copilot desktop application running as an AI agent
        new EnvironmentDetectionRuleWithResult<string>("copilot-app", new EnvironmentVariableValueRule("AI_AGENT", "github_copilot_app_agent")),
        // (proposed) generic flag for Agentic usage
        new EnvironmentDetectionRuleWithResult<string>("generic_agent", new BooleanEnvironmentRule("AGENT_CLI")),
    ];

    /// <inheritdoc/>
    public string? GetLLMEnvironment()
    {
        var results = _detectionRules.Select(r => r.GetResult()).Where(r => r != null).ToArray();
        return results.Length > 0 ? string.Join(", ", results) : null;
    }

    /// <inheritdoc/>
    public bool IsLLMEnvironment() => !string.IsNullOrEmpty(GetLLMEnvironment());
}
