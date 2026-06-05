// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Pure policy predicates that describe what a <see cref="PathPreference"/> implies. Kept in a
/// neutral location (next to <see cref="PathPreference"/>) so both the init workflow and the
/// default-resolution helpers can consume them without depending on each other.
/// </summary>
internal static class PathPreferencePolicy
{
    /// <summary>
    /// Returns true when the given <see cref="PathPreference"/> implies we should
    /// replace the default dotnet installation (i.e. update PATH / DOTNET_ROOT).
    /// </summary>
    public static bool ShouldReplaceSystemConfiguration(PathPreference preference) =>
        preference is PathPreference.FullPathReplacement;

    /// <summary>
    /// Returns true when the chosen mode shadows the system PATH and the user should therefore
    /// be offered migration of existing system-level .NET installs into dotnetup-managed installs.
    /// </summary>
    public static bool ShouldPromptToConvertSystemInstalls(PathPreference preference) =>
        preference != PathPreference.DotnetupDotnet;
}
