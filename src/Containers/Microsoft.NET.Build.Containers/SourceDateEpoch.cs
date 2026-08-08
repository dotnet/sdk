// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Provides the timestamp stamped into generated container artifacts.
/// </summary>
/// <remarks>
/// Image creation is otherwise a function of the current time: the image config's creation date, the
/// generated history entries and every layer tar entry are stamped with <see cref="DateTime.UtcNow"/>.
/// Publishing one commit twice therefore produces two different digests, which defeats content-addressed
/// deduplication in registries and makes downstream tooling treat a rebuild as a new artifact.
/// Honoring <see href="https://reproducible-builds.org/docs/source-date-epoch/">SOURCE_DATE_EPOCH</see>,
/// the cross-ecosystem convention for this, lets a build opt into a reproducible image.
/// </remarks>
internal static class SourceDateEpoch
{
    private const string EnvironmentVariableName = "SOURCE_DATE_EPOCH";

    /// <summary>
    /// The timestamp to stamp into generated container artifacts: the value of SOURCE_DATE_EPOCH when
    /// it is set to a valid non-negative integer, otherwise the current UTC time.
    /// </summary>
    internal static DateTime GetTimestamp(Func<string, string?>? environmentReader = null)
    {
        string? value = (environmentReader ?? Environment.GetEnvironmentVariable)(EnvironmentVariableName);

        // An unset variable is the common case, and a malformed one is treated the same way: the
        // reproducible-builds specification asks consumers to ignore values they cannot interpret
        // rather than fail the build.
        if (string.IsNullOrWhiteSpace(value) ||
            !long.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out long secondsSinceEpoch))
        {
            return DateTime.UtcNow;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(secondsSinceEpoch).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return DateTime.UtcNow;
        }
    }
}
