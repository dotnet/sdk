// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;

namespace Microsoft.DotNet.Cli.Telemetry;

internal class LLMEnvironmentDetectorForTelemetry : ILLMEnvironmentDetector
{
    private static readonly EnvironmentDetectionRuleWithResult<string>[] _detectionRules = [
        // Claude Code
        new EnvironmentDetectionRuleWithResult<string>("claude", new AnyPresentEnvironmentRule("CLAUDECODE", "CLAUDE_CODE_ENTRYPOINT")),
        // Cursor AI
        new EnvironmentDetectionRuleWithResult<string>("cursor", new AnyPresentEnvironmentRule("CURSOR_EDITOR", "CURSOR_AI")),
        // Gemini
        new EnvironmentDetectionRuleWithResult<string>("gemini", new BooleanEnvironmentRule("GEMINI_CLI")),
        // GitHub Copilot
        new EnvironmentDetectionRuleWithResult<string>("copilot", new BooleanEnvironmentRule("GITHUB_COPILOT_CLI_MODE")),
        // Codex CLI
        new EnvironmentDetectionRuleWithResult<string>("codex", new AnyPresentEnvironmentRule("CODEX_CLI", "CODEX_SANDBOX")),
        // Aider
        new EnvironmentDetectionRuleWithResult<string>("aider", new EnvironmentVariableValueRule("OR_APP_NAME", "Aider")),
        // Amp
        new EnvironmentDetectionRuleWithResult<string>("amp", new AnyPresentEnvironmentRule("AMP_HOME")),
        // Qwen Code
        new EnvironmentDetectionRuleWithResult<string>("qwen", new AnyPresentEnvironmentRule("QWEN_CODE")),
        // Droid
        new EnvironmentDetectionRuleWithResult<string>("droid", new BooleanEnvironmentRule("DROID_CLI")),
        // OpenCode
        new EnvironmentDetectionRuleWithResult<string>("opencode", new AnyPresentEnvironmentRule("OPENCODE_AI")),
        // Zed AI
        new EnvironmentDetectionRuleWithResult<string>("zed", new AnyPresentEnvironmentRule("ZED_ENVIRONMENT", "ZED_TERM")),
        // Kimi CLI
        new EnvironmentDetectionRuleWithResult<string>("kimi", new BooleanEnvironmentRule("KIMI_CLI")),
        // OpenHands
        new EnvironmentDetectionRuleWithResult<string>("openhands", new EnvironmentVariableValueRule("OR_APP_NAME", "OpenHands")),
        // Goose
        new EnvironmentDetectionRuleWithResult<string>("goose", new AnyPresentEnvironmentRule("GOOSE_TERMINAL")),
        // Cline
        new EnvironmentDetectionRuleWithResult<string>("cline", new AnyPresentEnvironmentRule("CLINE_TASK_ID")),
        // Roo Code
        new EnvironmentDetectionRuleWithResult<string>("roo", new AnyPresentEnvironmentRule("ROO_CODE_TASK_ID")),
        // Windsurf
        new EnvironmentDetectionRuleWithResult<string>("windsurf", new AnyPresentEnvironmentRule("WINDSURF_SESSION")),
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