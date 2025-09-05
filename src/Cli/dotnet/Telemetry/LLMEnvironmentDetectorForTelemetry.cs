// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli.Telemetry;

internal class LLMEnvironmentDetectorForTelemetry : ILLMEnvironmentDetector
{
    // Systems where the variable must be present and not-null
    private static readonly string[] _ifNonNullVariables = [
        // Claude Code
        "CLAUDECODE"
    ];

    public bool IsLLMEnvironment()
    {
        foreach (var variable in _ifNonNullVariables)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable)))
            {
                return true;
            }
        }

        return false;
    }
}