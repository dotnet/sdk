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

    /// <summary>
    /// Returns the display name without any trailing parenthetical recommendation hint
    /// (e.g. drops the "(Suggested)" from "Terminal Mode (Suggested)"). Used when showing a
    /// preference the user has already chosen, where a recommendation hint would be misleading.
    /// </summary>
    public static string GetNameWithoutHint(PathPreference preference)
    {
        string name = GetName(preference);
        int hintIndex = name.IndexOf(" (", System.StringComparison.Ordinal);
        return hintIndex >= 0 ? name[..hintIndex] : name;
    }
}
