// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Contains conservative probe state and, when available, exact serialized directives.
/// </summary>
/// <param name="ProbeResult">The conservative directive-probe result.</param>
/// <param name="CanCache">Whether the directive set permits cache persistence.</param>
/// <param name="Directives">The serialized directives recognized by the SDK.</param>
internal sealed record FileBasedAppDirectiveInfo(
    FileBasedAppDirectiveProbeResult ProbeResult,
    bool CanCache,
    ImmutableArray<string> Directives);
