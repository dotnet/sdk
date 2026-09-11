// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Telemetry;

namespace Microsoft.DotNet.Cli;

/// <summary>
/// Immutable service dependencies for a command and the subordinate commands it creates.
/// </summary>
public sealed class CommandServices(ILLMEnvironmentDetector? llmEnvironmentDetector = null)
{
    public ILLMEnvironmentDetector LLMEnvironmentDetector { get; } =
        llmEnvironmentDetector ?? new LLMEnvironmentDetectorForTelemetry();
}
