// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Pure policy predicates that describe what a <see cref="DotnetAccessMode"/> implies. Kept in a
/// neutral location (next to <see cref="DotnetAccessMode"/>) so both the init workflow and the
/// default-resolution helpers can consume them without depending on each other.
/// </summary>
internal static class DotnetAccessModePolicy
{
    /// <summary>
    /// Returns true when the given <see cref="DotnetAccessMode"/> implies we should
    /// replace the default dotnet installation (i.e. update PATH / DOTNET_ROOT).
    /// </summary>
    public static bool ShouldReplaceSystemConfiguration(DotnetAccessMode accessMode) =>
        accessMode is DotnetAccessMode.Everywhere;

    /// <summary>
    /// Returns true when the chosen mode shadows the system PATH and the user should therefore
    /// be offered migration of existing system-level .NET installs into dotnetup-managed installs.
    /// </summary>
    public static bool ShouldPromptToConvertSystemInstalls(DotnetAccessMode accessMode) =>
        accessMode != DotnetAccessMode.None;

    /// <summary>
    /// Returns true when the mode can only be applied on Windows. <see cref="DotnetAccessMode.Everywhere"/>
    /// edits user-level env-var PATH/DOTNET_ROOT, which dotnetup only manages on Windows today. This is
    /// the single source of truth for the platform constraint — every entry point that validates a mode
    /// should consult it rather than re-deriving the assumption.
    /// </summary>
    public static bool RequiresWindows(DotnetAccessMode accessMode) =>
        accessMode is DotnetAccessMode.Everywhere;

    /// <summary>
    /// Returns true when the mode can be applied on the current platform.
    /// </summary>
    public static bool IsSupportedOnCurrentPlatform(DotnetAccessMode accessMode) =>
        !RequiresWindows(accessMode) || OperatingSystem.IsWindows();
}
