// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Provides image metadata and content blobs independently of where an image is stored.
/// </summary>
internal interface IImageSource
{
    /// <summary>
    /// Resolves an image reference and creates a builder from its platform-specific manifest.
    /// </summary>
    Task<ImageBuilder> GetImageManifestAsync(
        string repositoryName,
        string reference,
        string runtimeIdentifier,
        IManifestPicker manifestPicker,
        CancellationToken cancellationToken);

    /// <summary>
    /// Makes a content blob available as a local file.
    /// </summary>
    Task<string> GetBlobPathAsync(
        string repository,
        Descriptor descriptor,
        CancellationToken cancellationToken);
}
