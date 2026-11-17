// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// The current evaluated version spec and matching installations for a saved spec. Missing or
/// versionless repository files are inactive; unreadable requirements remain active
/// with a diagnostic, using the last known requirement when available.
/// </summary>
internal sealed class InstallSpecResolution
{
    public bool IsActive { get; init; } = true;
    /// <summary>
    /// The current or last known version specification.
    /// Null when inactive or unreadable without a saved requirement.
    /// </summary>
    public UpdateChannel? Spec { get; init; }
    public GlobalJsonContents.SdkSection? GlobalJsonSdk { get; init; }
    /// <summary>Eligible installations ordered newest first.</summary>
    public IReadOnlyList<Installation> MatchingInstallations { get; set; } = [];
    public Installation? Installation => MatchingInstallations.Count > 0 ? MatchingInstallations[0] : null;
    public string? Error { get; init; }
    /// <summary>The read or parse failure behind <see cref="Error"/>, so callers can report its kind.</summary>
    public Exception? ErrorException { get; init; }
    public bool UsesLastKnownRequirement => Error is not null && Spec is not null;
}
