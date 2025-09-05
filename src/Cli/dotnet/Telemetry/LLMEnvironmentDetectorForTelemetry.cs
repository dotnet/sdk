// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli.Telemetry;

internal class LLMEnvironmentDetectorForTelemetry : ILLMEnvironmentDetector
{
    // Systems where the variable must be present and not-null
    private static readonly string[] _claudeVariables = [
        // Claude Code
        "CLAUDECODE"
    ];

    public string GetLLMEnvironment()
    {
        foreach (var variable in _claudeVariables)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable)))
            {
                return "claude";
            }
        }

        return null;
    }
}