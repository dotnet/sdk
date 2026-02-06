// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Telemetry;

internal interface ILLMEnvironmentDetector
{
    /// <summary>
    /// Checks the current environment for known indicators of LLM usage and returns a string identifying the LLM environment if detected.
    /// </summary>
    string? GetLLMEnvironment();

    /// <summary>
    /// Returns true if the current environment is detected to be an LLM/agentic environment, false otherwise.
    /// </summary>
    bool IsLLMEnvironment();
}
