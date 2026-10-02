// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.InternalMicrosoft;

/// <summary>
/// Defines the orchestration policy for one detector.
/// Production uses the default value, while tests replace complete policy values.
/// </summary>
internal sealed record InternalMicrosoftDetectorOptions(
    Func<InternalMicrosoftDetectionContext, IReadOnlyList<IReadOnlyList<InternalMicrosoftProbe>>> CreateProbeStages,
    TimeSpan ProbeStageTimeout,
    Func<TimeSpan, CancellationTokenSource> CreateProbeStageTimeoutSource)
{
    internal static InternalMicrosoftDetectorOptions Default { get; } = new(
        CreateProbeStages: CreateDefaultProbeStages,
        ProbeStageTimeout: TimeSpan.FromSeconds(5),
        CreateProbeStageTimeoutSource: static timeout => new CancellationTokenSource(timeout));

    private static IReadOnlyList<IReadOnlyList<InternalMicrosoftProbe>> CreateDefaultProbeStages(
        InternalMicrosoftDetectionContext context) =>
        InternalMicrosoftDetector.CreateDefaultProbeStages(context);
}
