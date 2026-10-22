// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>Where the effective access mode came from.</summary>
internal enum AccessModeSource
{
    /// <summary>No access mode is configured.</summary>
    Unset,

    /// <summary>The access mode recorded in the per-user dotnetup config.</summary>
    UserConfig,
}

/// <summary>The access mode dotnetup operates under, and its source.</summary>
internal sealed record EffectiveAccessMode(DotnetAccessMode? Mode, AccessModeSource Source)
{
    public bool IsAdmin => Mode == DotnetAccessMode.Admin;

    public static EffectiveAccessMode Unset { get; } = new(null, AccessModeSource.Unset);
}

/// <summary>
/// Resolves the effective access mode from the per-user dotnetup config.
/// </summary>
internal static class AccessModeResolver
{
    /// <summary>
    /// Resolves the effective access mode. Writes no output and never throws. Admin mode recorded
    /// on a platform that does not support it is ignored.
    /// </summary>
    public static EffectiveAccessMode Resolve()
    {
        DotnetAccessMode? configured = TryReadUserConfigAccessMode();
        if (configured == DotnetAccessMode.Admin && !DotnetAccessModePolicy.IsSupportedOnCurrentPlatform(DotnetAccessMode.Admin))
        {
            return EffectiveAccessMode.Unset;
        }

        return configured is DotnetAccessMode mode
            ? new EffectiveAccessMode(mode, AccessModeSource.UserConfig)
            : EffectiveAccessMode.Unset;
    }

    private static DotnetAccessMode? TryReadUserConfigAccessMode()
    {
        try
        {
            return DotnetupConfig.ReadAccessModeSilently();
        }
        catch
        {
            return null;
        }
    }
}
