// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;

namespace Microsoft.DotNet.Cli.Telemetry;

internal class LLMEnvironmentDetectorForTelemetry : ILLMEnvironmentDetector
{
    private static readonly EnvironmentDetectionRuleWithResult<string>[] _detectionRules = [
        // Claude Code
        new EnvironmentDetectionRuleWithResult<string>("claude", "CLAUDECODE")
    ];

    public string? GetLLMEnvironment()
    {
        return _detectionRules.Select(rule => rule.GetResult()).FirstOrDefault(result => result != null);
    }
}