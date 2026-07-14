// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

internal enum ContainerRuntimeKind
{
    Unknown,

    Docker,

    Podman,

    Wslc
}

/// <summary>
/// Defines the runtime-specific behavior required to discover a local container runtime
/// and load built images into its image store.
/// </summary>
internal interface IContainerRuntime
{
    /// <summary>
    /// Performs the lightweight command probe used when automatically selecting a runtime.
    /// </summary>
    /// <returns><see langword="true"/> when the runtime command responded successfully; otherwise, <see langword="false"/>.</returns>
    Task<bool> ProbeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether the runtime is ready to accept images, including any runtime-specific daemon checks.
    /// </summary>
    /// <returns><see langword="true"/> when the runtime is available; otherwise, <see langword="false"/>.</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Selects the manifest media type supported by this runtime for the requested image format.
    /// </summary>
    /// <param name="defaultManifestMediaType">The media type inferred from the source image.</param>
    /// <param name="imageFormat">The explicitly requested image format, or <see langword="null"/> to preserve the source format.</param>
    /// <returns>The manifest media type to use when building the image.</returns>
    string GetManifestMediaType(string defaultManifestMediaType, KnownImageFormats? imageFormat);

    /// <summary>
    /// Gets the stable runtime classification reported through SDK telemetry.
    /// </summary>
    ContainerRuntimeKind GetTelemetryValue();

    /// <summary>
    /// Loads a single-platform image into this runtime's local image store, retrieving inherited
    /// layers from the source reference and assigning the destination repository and tags.
    /// </summary>
    Task LoadAsync(
        BuiltImage image,
        SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads a multi-platform image into this runtime's local image store, retrieving inherited
    /// layers from the source reference and assigning the destination repository and tags.
    /// </summary>
    Task LoadAsync(
        MultiArchImage image,
        SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        CancellationToken cancellationToken);
}
