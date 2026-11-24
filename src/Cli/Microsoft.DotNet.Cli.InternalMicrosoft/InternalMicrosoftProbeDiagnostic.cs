// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.InternalMicrosoft;

/// <summary>
/// Records non-sensitive health information for one completed probe.
/// </summary>
internal sealed record InternalMicrosoftProbeDiagnostic(
    string Source,
    string Outcome,
    TimeSpan Duration,
    bool HasAlias,
    bool HasDomain,
    InternalMicrosoftProbeFailure? Failure = null);
