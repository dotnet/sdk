// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Extensions for attaching follow-on (best-effort batch) failures to a
/// primary exception. Used by workflows that keep going after the first
/// failure (e.g., updating multiple specs, installing multiple components)
/// to collect siblings without losing or duplicating the primary failure.
/// </summary>
/// <remarks>
/// <para>
/// Storage is on <see cref="Exception.Data"/> under a single private key,
/// so the list survives <c>throw;</c> rethrows and works for any exception
/// type (no new exception class required). Callers should treat the
/// storage as opaque — read via <see cref="GetAdditionalFailures"/> /
/// <see cref="GetTruncatedAdditionalFailureCount"/> rather than touching
/// <see cref="Exception.Data"/> directly.
/// </para>
/// <para>
/// Capped at <see cref="MaxAdditionalFailures"/> entries to keep downstream
/// payloads bounded (e.g., AppInsights property-value 8 KB limit). Overflow
/// failures are not stored individually but are counted via
/// <see cref="GetTruncatedAdditionalFailureCount"/>.
/// </para>
/// </remarks>
internal static class ExceptionExtensions
{
    /// <summary>
    /// Maximum number of additional failures retained on the primary
    /// exception. Further attaches only increment the truncated counter.
    /// </summary>
    public const int MaxAdditionalFailures = 10;

    private const string AdditionalFailuresKey = "dotnetup.additional_failures";
    private const string TruncatedCountKey = "dotnetup.additional_failures.truncated";

    /// <summary>
    /// Attaches a follow-on failure to <paramref name="primary"/>. The
    /// first <see cref="MaxAdditionalFailures"/> attachments are stored;
    /// subsequent calls only increment the truncated counter readable via
    /// <see cref="GetTruncatedAdditionalFailureCount"/>.
    /// </summary>
    public static void AttachAdditionalFailure(this Exception primary, Exception other)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(other);

        if (primary.Data[AdditionalFailuresKey] is not List<Exception> list)
        {
            list = [];
            primary.Data[AdditionalFailuresKey] = list;
        }

        if (list.Count < MaxAdditionalFailures)
        {
            list.Add(other);
        }
        else
        {
            int prior = primary.Data[TruncatedCountKey] is int n ? n : 0;
            primary.Data[TruncatedCountKey] = prior + 1;
        }
    }

    /// <summary>
    /// Returns the attached follow-on failures (in attach order). Empty
    /// when none have been attached.
    /// </summary>
    public static IReadOnlyList<Exception> GetAdditionalFailures(this Exception primary)
    {
        ArgumentNullException.ThrowIfNull(primary);
        return primary.Data[AdditionalFailuresKey] as List<Exception> ?? (IReadOnlyList<Exception>)Array.Empty<Exception>();
    }

    /// <summary>
    /// Returns the number of follow-on failures that were dropped because
    /// <see cref="MaxAdditionalFailures"/> was already reached. Zero when
    /// no overflow occurred.
    /// </summary>
    public static int GetTruncatedAdditionalFailureCount(this Exception primary)
    {
        ArgumentNullException.ThrowIfNull(primary);
        return primary.Data[TruncatedCountKey] is int n ? n : 0;
    }
}
