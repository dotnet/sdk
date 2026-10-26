// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// The current evaluated version spec and selected installation for a saved spec. Missing or
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
    public Installation? Installation { get; set; }
    public string? Error { get; init; }
    public bool UsesFallbackRequirement => Error is not null && Spec is not null;
}
