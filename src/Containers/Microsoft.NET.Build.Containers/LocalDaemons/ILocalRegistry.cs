// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Defines the operations used to publish built images to a local container image store or archive.
/// </summary>
internal interface ILocalRegistry
{
    /// <summary>
    /// Loads a single-platform image, retrieving inherited layers from the source reference and
    /// assigning the destination repository and tags.
    /// </summary>
    public Task LoadAsync(BuiltImage image, SourceImageReference sourceReference, DestinationImageReference destinationReference, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a multi-platform image, retrieving inherited layers from the source reference and
    /// assigning the destination repository and tags.
    /// </summary>
    public Task LoadAsync(MultiArchImage multiArchImage, SourceImageReference sourceReference, DestinationImageReference destinationReference, CancellationToken cancellationToken);

    /// <summary>
    /// Determines asynchronously whether the destination is ready to receive images.
    /// </summary>
    /// <returns><see langword="true"/> when the destination is available; otherwise, <see langword="false"/>.</returns>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Determines synchronously whether the destination is ready to receive images.
    /// </summary>
    /// <returns><see langword="true"/> when the destination is available; otherwise, <see langword="false"/>.</returns>
    public bool IsAvailable();
}
