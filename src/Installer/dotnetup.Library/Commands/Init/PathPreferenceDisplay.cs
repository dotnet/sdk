// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// Maps a <see cref="PathPreference"/> to its user-facing display name.
/// </summary>
internal static class PathPreferenceDisplay
{
    /// <summary>
    /// Returns the localized display name for the given <see cref="PathPreference"/>.
    /// </summary>
    public static string GetName(PathPreference preference) => preference switch
    {
        PathPreference.DotnetupDotnet => Strings.PathPreferenceDotnetupDotnet,
        PathPreference.ShellProfile => Strings.PathPreferenceShellProfile,
        PathPreference.FullPathReplacement => Strings.PathPreferenceFullReplacement,
        _ => preference.ToString(),
    };
}
