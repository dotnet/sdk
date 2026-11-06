// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.InternalMicrosoft;

/// <summary>
/// Describes the final classification, cache state, and provider diagnostics from one detector run.
/// </summary>
internal sealed record InternalMicrosoftDetectionResult(
    bool IsInternalMicrosoft,
    string? Source,
    string? Alias,
    string? Domain,
    bool IsCIEnvironment,
    string Outcome,
    string CacheStatus,
    TimeSpan Duration,
    IReadOnlyList<InternalMicrosoftProbeDiagnostic> ProbeDiagnostics);
