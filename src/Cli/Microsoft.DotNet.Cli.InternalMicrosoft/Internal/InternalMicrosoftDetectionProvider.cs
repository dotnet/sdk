// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.InternalMicrosoft;

/// <summary>
/// Defines one independent source of Microsoft-internal evidence.
/// Provider instances are stateless and can be reused for each process-wide detector run.
/// </summary>
internal interface IInternalMicrosoftDetectionProvider
{
    /// <summary>
    /// Gets the stable source name included in detector results and diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the ordered stage. A positive result prevents later stages from running.
    /// </summary>
    int Stage { get; }

    /// <summary>
    /// Returns true when the current platform and environment support this provider.
    /// </summary>
    bool IsSupported(InternalMicrosoftDetectionContext context);

    /// <summary>
    /// Checks one source of evidence within the detector stage deadline.
    /// </summary>
    Task<InternalMicrosoftProbeResult> DetectAsync(
        InternalMicrosoftDetectionContext context,
        CancellationToken cancellationToken);
}
