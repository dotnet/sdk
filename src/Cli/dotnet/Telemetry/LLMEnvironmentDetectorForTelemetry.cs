// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;

namespace Microsoft.DotNet.Cli.Telemetry;

internal class LLMEnvironmentDetectorForTelemetry : ILLMEnvironmentDetector
{
    // Most rules are presence-based (a variable being set/non-empty is enough); a few use exact-value
    // matching (e.g. OR_APP_NAME, which several tools share). All matching rules contribute to the result.
    private static readonly EnvironmentDetectionRuleWithResult<string>[] _detectionRules = [
        // Cowork (Claude Code cowork mode) - placed before Claude so the more specific variable is listed first
        new EnvironmentDetectionRuleWithResult<string>("cowork", new AnyPresentEnvironmentRule("CLAUDE_CODE_IS_COWORK")),
        // Claude Code
        new EnvironmentDetectionRuleWithResult<string>("claude", new AnyPresentEnvironmentRule("CLAUDECODE", "CLAUDE_CODE", "CLAUDE_CODE_ENTRYPOINT")),
        // Cursor AI
        new EnvironmentDetectionRuleWithResult<string>("cursor", new AnyPresentEnvironmentRule("CURSOR_EDITOR", "CURSOR_AI", "CURSOR_TRACE_ID", "CURSOR_AGENT")),
        // Gemini
        new EnvironmentDetectionRuleWithResult<string>("gemini", new AnyPresentEnvironmentRule("GEMINI_CLI")),
        // GitHub Copilot CLI (legacy gh extension: GITHUB_COPILOT_CLI_MODE; new Copilot CLI: GH_COPILOT_WORKING_DIRECTORY, COPILOT_CLI, COPILOT_MODEL, COPILOT_ALLOW_ALL, or COPILOT_GITHUB_TOKEN is set).
        new EnvironmentDetectionRuleWithResult<string>("copilot-cli", new AnyPresentEnvironmentRule(
            "GITHUB_COPILOT_CLI_MODE", "GH_COPILOT_WORKING_DIRECTORY", "COPILOT_CLI", "COPILOT_MODEL", "COPILOT_ALLOW_ALL", "COPILOT_GITHUB_TOKEN")),
        // GitHub Copilot agent mode in VS Code, which sets AI_AGENT=github_copilot_vscode_agent and COPILOT_AGENT=1 on the terminals it runs commands in.
        // See https://github.com/microsoft/vscode/blob/main/src/vs/workbench/contrib/terminalContrib/chatAgentTools/browser/toolTerminalCreator.ts
        new EnvironmentDetectionRuleWithResult<string>("copilot-vscode", new AnyMatchEnvironmentRule(
            new EnvironmentVariableValueRule("AI_AGENT", "github_copilot_vscode_agent"),
            new AnyPresentEnvironmentRule("COPILOT_AGENT"))),
        // Codex CLI
        new EnvironmentDetectionRuleWithResult<string>("codex", new AnyPresentEnvironmentRule("CODEX_CLI", "CODEX_SANDBOX", "CODEX_CI", "CODEX_THREAD_ID")),
        // Aider
        new EnvironmentDetectionRuleWithResult<string>("aider", new EnvironmentVariableValueRule("OR_APP_NAME", "Aider")),
        // Plandex
        new EnvironmentDetectionRuleWithResult<string>("plandex", new EnvironmentVariableValueRule("OR_APP_NAME", "plandex")),
        // Amp
        new EnvironmentDetectionRuleWithResult<string>("amp", new AnyPresentEnvironmentRule("AMP_HOME")),
        // Qwen Code
        new EnvironmentDetectionRuleWithResult<string>("qwen", new AnyPresentEnvironmentRule("QWEN_CODE")),
        // Droid
        new EnvironmentDetectionRuleWithResult<string>("droid", new AnyPresentEnvironmentRule("DROID_CLI")),
        // OpenCode
        new EnvironmentDetectionRuleWithResult<string>("opencode", new AnyPresentEnvironmentRule("OPENCODE_AI")),
        // Zed AI
        new EnvironmentDetectionRuleWithResult<string>("zed", new AnyPresentEnvironmentRule("ZED_ENVIRONMENT", "ZED_TERM")),
        // Kimi CLI
        new EnvironmentDetectionRuleWithResult<string>("kimi", new AnyPresentEnvironmentRule("KIMI_CLI")),
        // OpenHands
        new EnvironmentDetectionRuleWithResult<string>("openhands", new EnvironmentVariableValueRule("OR_APP_NAME", "OpenHands")),
        // Goose
        new EnvironmentDetectionRuleWithResult<string>("goose", new AnyPresentEnvironmentRule("GOOSE_TERMINAL", "GOOSE_PROVIDER")),
        // Cline
        new EnvironmentDetectionRuleWithResult<string>("cline", new AnyPresentEnvironmentRule("CLINE_TASK_ID")),
        // Roo Code
        new EnvironmentDetectionRuleWithResult<string>("roo", new AnyPresentEnvironmentRule("ROO_CODE_TASK_ID")),
        // Windsurf
        new EnvironmentDetectionRuleWithResult<string>("windsurf", new AnyPresentEnvironmentRule("WINDSURF_SESSION")),
        // Replit
        new EnvironmentDetectionRuleWithResult<string>("replit", new AnyPresentEnvironmentRule("REPL_ID")),
        // Augment
        new EnvironmentDetectionRuleWithResult<string>("augment", new AnyPresentEnvironmentRule("AUGMENT_AGENT")),
        // Antigravity
        new EnvironmentDetectionRuleWithResult<string>("antigravity", new AnyPresentEnvironmentRule("ANTIGRAVITY_AGENT")),
        // (proposed) generic flag for Agentic usage
        new EnvironmentDetectionRuleWithResult<string>("generic_agent", new AnyPresentEnvironmentRule("AGENT_CLI")),
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
