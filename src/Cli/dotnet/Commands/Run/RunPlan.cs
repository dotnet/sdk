// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Describes the selected run tier, its reason, and any reusable cache or launch contract.
/// </summary>
/// <param name="Tier">The selected run tier.</param>
/// <param name="Reason">The reason the tier was selected.</param>
/// <param name="Cache">The computed cache state, or <see langword="null"/>.</param>
/// <param name="Launch">The validated launch contract, or <see langword="null"/>.</param>
internal sealed record RunPlan(
    RunTier Tier,
    RunDecisionReason Reason,
    FileBasedAppCacheInfo? Cache,
    FileBasedAppLaunchInfo? Launch = null)
{
    /// <summary>
    /// Maps a managed build-planning tier to its existing build level.
    /// </summary>
    /// <returns>The corresponding managed build level.</returns>
    /// <exception cref="InvalidOperationException">The run tier does not represent managed build planning.</exception>
    internal BuildLevel ToBuildLevel()
        => Tier switch
        {
            RunTier.CachedLaunch => BuildLevel.None,
            RunTier.DirectCompile => BuildLevel.Csc,
            RunTier.MSBuildBuild => BuildLevel.All,
            _ => throw new InvalidOperationException($"Run tier '{Tier}' does not map to a managed build level."),
        };
}
