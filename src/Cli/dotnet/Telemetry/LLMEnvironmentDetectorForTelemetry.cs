// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;

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
        // GitHub Copilot
        new EnvironmentDetectionRuleWithResult<string>("copilot", new BooleanEnvironmentRule("GITHUB_COPILOT_CLI_MODE")),
        // (proposed) generic flag for Agentic usage
        new EnvironmentDetectionRuleWithResult<string>("generic_agent", new BooleanEnvironmentRule("AGENT_CLI")),
    ];

    public string? GetLLMEnvironment()
    {
        var results = _detectionRules.Select(r => r.GetResult()).Where(r => r != null).ToArray();
        return results.Length > 0 ? string.Join(", ", results) : null;
    }
}
