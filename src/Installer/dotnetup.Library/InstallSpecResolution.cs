// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;
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
    /// <summary>Warning text for commands that continue past an unreadable requirement.</summary>
    public string? Error { get; init; }
    /// <summary>
    /// The user-facing error for an unreadable requirement: <see cref="DotnetInstallErrorCode.MalformedGlobalJson"/>
    /// for invalid contents, otherwise <see cref="DotnetInstallErrorCode.ContextResolutionFailed"/>.
    /// Callers that cannot use the last known requirement throw it.
    /// </summary>
    public DotnetInstallException? Failure { get; init; }
    public bool UsesLastKnownRequirement => Error is not null && Spec is not null;
}
