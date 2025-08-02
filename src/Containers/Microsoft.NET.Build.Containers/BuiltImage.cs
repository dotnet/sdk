// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Represents constructed image ready for further processing.
/// </summary>
internal readonly struct BuiltImage
{
    /// <summary>
    /// Gets image configuration in JSON format.
    /// </summary>
    internal required JsonObject Config { get; init; }

    /// <summary>
    /// Gets image manifest.
    /// </summary>
    internal required ManifestV2 Manifest { get; init; }

    /// <summary>
    /// Gets manifest digest.
    /// </summary>
    internal Digest ManifestDigest => Manifest.GetDigest();

    /// <summary>
    /// Gets manifest mediaType.
    /// </summary>
    internal string ManifestMediaType => Manifest.MediaType!;

    /// <summary>
    /// Gets image layers.
    /// </summary>
    internal List<Descriptor>? Layers { get; init; }

    /// <summary>
    /// Gets image OS.
    /// </summary>
    internal string? OS { get; init; }

    /// <summary>
    /// Gets image architecture.
    /// </summary>
    internal string? Architecture { get; init; }
}
