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
}
