// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Telemetry;

internal class LLMEnvironmentDetectorForTelemetry : ILLMEnvironmentDetector
{
    private static readonly EnvironmentDetectionRuleWithResult<string>[] _detectionRules = [
        // Claude Code
        new EnvironmentDetectionRuleWithResult<string>("claude", new BooleanEnvironmentRule("CLAUDECODE")),
        // Cursor AI
        new EnvironmentDetectionRuleWithResult<string>("cursor", new BooleanEnvironmentRule("CURSOR_EDITOR")),
        // Gemini
        new EnvironmentDetectionRuleWithResult<string>("gemini", new BooleanEnvironmentRule("GEMINI_CLI")),
        // GitHub Copilot
        new EnvironmentDetectionRuleWithResult<string>("copilot", new BooleanEnvironmentRule("GITHUB_COPILOT_CLI_MODE")),
        // OpenAI Codex
        new EnvironmentDetectionRuleWithResult<string>("codex", new BooleanEnvironmentRule("CODEX_THREAD_ID")),
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
