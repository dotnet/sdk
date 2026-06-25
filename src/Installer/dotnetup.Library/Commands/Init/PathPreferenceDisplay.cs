// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// Maps a <see cref="PathPreference"/> to its user-facing display name.
/// </summary>
internal static class PathPreferenceDisplay
{
    /// <summary>
    /// Returns the localized display name for the given <see cref="PathPreference"/>.
    /// The name does not include any recommendation hint; use <see cref="GetNameWithSuggestedHint"/>
    /// when showing the recommended preference.
    /// </summary>
    public static string GetName(PathPreference preference) => preference switch
    {
        PathPreference.DotnetupDotnet => Strings.PathPreferenceDotnetupDotnet,
        PathPreference.ShellProfile => Strings.PathPreferenceShellProfile,
        PathPreference.FullPathReplacement => Strings.PathPreferenceFullReplacement,
        _ => preference.ToString(),
    };

    /// <summary>
    /// Returns the display name, appending the localized "(Suggested)" hint only for the suggested
    /// mode (<see cref="PathPreference.ShellProfile"/> / Terminal Mode). The hint is a separate,
    /// independently localizable string (rather than baked into the name) so translations are not
    /// forced to follow the "(...)" convention or a particular word order.
    /// </summary>
    public static string GetNameWithSuggestedHint(PathPreference preference)
        => preference is PathPreference.ShellProfile
            ? string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1}",
                GetName(preference),
                Strings.PathPreferenceSuggestedSuffix)
            : GetName(preference);
}
