// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.InternalMicrosoft;

/// <summary>
/// Classifies whether the current environment has Microsoft-internal evidence.
/// Consumers normally create one implementation for the process lifetime.
/// </summary>
internal interface IInternalMicrosoftDetector
{
    /// <summary>
    /// Resolves one classification result or returns the cached result.
    /// </summary>
    Task<InternalMicrosoftDetectionResult> IsInternalMicrosoftMachineAsync(CancellationToken cancellationToken = default);
}
