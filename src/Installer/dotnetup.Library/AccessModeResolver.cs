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

    /// <summary>The machine policy (admin mode).</summary>
    Policy,
}

/// <summary>The access mode dotnetup operates under, and its source.</summary>
internal sealed record EffectiveAccessMode(DotnetAccessMode? Mode, AccessModeSource Source)
{
    public bool IsAdmin => Mode == DotnetAccessMode.Admin;

    public static EffectiveAccessMode Unset { get; } = new(null, AccessModeSource.Unset);
}

/// <summary>
/// Resolves the effective access mode. The Windows machine policy enables admin mode and takes
/// precedence over the per-user config, which can never select admin mode.
/// </summary>
internal static class AccessModeResolver
{
    /// <summary>When non-null, replaces resolution. Tests must reset it to <c>null</c>.</summary>
    internal static Func<EffectiveAccessMode>? OverrideForTesting { get; set; }

    /// <summary>Resolves the effective access mode. Never throws and never writes output.</summary>
    public static EffectiveAccessMode Resolve()
    {
        var overrideFn = OverrideForTesting;
        if (overrideFn is not null)
        {
            return overrideFn();
        }

        if (OperatingSystem.IsWindows() && DotnetupPolicy.IsAdminModeEnabled())
        {
            return new EffectiveAccessMode(DotnetAccessMode.Admin, AccessModeSource.Policy);
        }

        DotnetAccessMode? configured = TryReadUserConfigAccessMode();
        return configured is DotnetAccessMode mode && mode != DotnetAccessMode.Admin
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
