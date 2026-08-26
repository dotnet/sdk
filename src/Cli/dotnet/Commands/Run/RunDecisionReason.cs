// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Identifies the reason a file-based application run tier was selected.
/// </summary>
internal enum RunDecisionReason
{
    /// <summary>The cached build and launch contract remains valid.</summary>
    CacheValid,

    /// <summary>Inputs changed but direct compilation is sufficient.</summary>
    DirectCompilationRequired,

    /// <summary>The invocation requires a full MSBuild build.</summary>
    FullBuildRequired,

    /// <summary>A no-build invocation can reuse a synthetic CSC cache.</summary>
    NoBuildSyntheticCache,

    /// <summary>A no-build invocation does not have an eligible synthetic cache.</summary>
    NoBuildNotEligible,

    /// <summary>An Executable launch profile supplies the launch contract.</summary>
    ExecutableLaunchProfile,

    /// <summary>The authoritative cached launch contract is incomplete or stale.</summary>
    CachedLaunchNotEligible,
}
