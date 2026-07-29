// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Identifies the implementation tier selected for a file-based application run.
/// </summary>
internal enum RunTier
{
    /// <summary>Launch existing output without validating build inputs.</summary>
    LaunchOnly,

    /// <summary>Launch an output whose cached build contract is still valid.</summary>
    CachedLaunch,

    /// <summary>Compile directly without MSBuild.</summary>
    DirectCompile,

    /// <summary>Evaluate the virtual project without building it.</summary>
    EvaluateOnly,

    /// <summary>Build through MSBuild.</summary>
    MSBuildBuild,

    /// <summary>Run through the MSBuild <c>Run</c> target.</summary>
    MSBuildRun,

    /// <summary>Defer the invocation to the managed CLI.</summary>
    ManagedFallback,
}
