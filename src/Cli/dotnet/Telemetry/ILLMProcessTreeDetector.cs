// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Telemetry;

internal interface ILLMProcessTreeDetector
{
    /// <summary>
    /// Walks the process tree to detect known LLM process names among ancestors of the current process.
    /// Returns a comma-separated list of detected LLM identifiers, or null if none found.
    /// </summary>
    string? GetLLMFromProcessTree();
}
