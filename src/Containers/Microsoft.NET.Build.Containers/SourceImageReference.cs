// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Represents a reference to a Docker image. A reference is made of a registry, a repository (aka the image name) and a tag or digest.
/// </summary>
internal readonly record struct SourceImageReference(Registry? Registry, string Repository, string? Tag, string? Digest)
{
    public override string ToString()
    {
        string sourceImageReference = Repository;

        if (Registry is { } reg)
        {
            sourceImageReference = $"{reg.RegistryName}/{sourceImageReference}";
        }

        if (!string.IsNullOrEmpty(Tag))
        {
            sourceImageReference = $"{sourceImageReference}:{Tag}";
        }

        if (!string.IsNullOrEmpty(Digest))
        {
            sourceImageReference = $"{sourceImageReference}@{Digest}";
        }

        return sourceImageReference;
    }

    /// <summary>
    /// Returns the repository and tag as a formatted string. Used in cases
    /// </summary>
    public string Reference
          => !string.IsNullOrEmpty(Digest) ? Digest : !string.IsNullOrEmpty(Tag) ? Tag : "latest";
}
