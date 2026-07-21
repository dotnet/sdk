// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// Maps a <see cref="DotnetAccessMode"/> to its user-facing display name.
/// </summary>
internal static class DotnetAccessModeDisplay
{
    /// <summary>
    /// Returns the localized display name for the given <see cref="DotnetAccessMode"/>.
    /// The name does not include any recommendation hint; use <see cref="GetNameWithSuggestedHint"/>
    /// when showing the recommended access mode.
    /// </summary>
    public static string GetName(DotnetAccessMode accessMode) => accessMode switch
    {
        DotnetAccessMode.None => Strings.AccessModeNone,
        DotnetAccessMode.Shell => Strings.AccessModeShell,
        DotnetAccessMode.Everywhere => Strings.AccessModeEverywhere,
        _ => accessMode.ToString(),
    };

    /// <summary>
    /// Returns the display name, appending the localized "(Suggested)" hint only when the given
    /// mode is the current default (see <see cref="InitWorkflowDefaults.GetDefaultAccessMode"/>).
    /// The hint is a separate, independently localizable string (rather than baked into the name)
    /// so translations are not forced to follow the "(...)" convention or a particular word order.
    /// </summary>
    public static string GetNameWithSuggestedHint(DotnetAccessMode accessMode)
        => accessMode == InitWorkflowDefaults.GetDefaultAccessMode()
            ? string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1}",
                GetName(accessMode),
                Strings.PathPreferenceSuggestedSuffix)
            : GetName(accessMode);
}
