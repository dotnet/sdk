// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Represents a reference to a Docker image. A reference is made of a registry, a repository (aka the image name) and a tag.
/// </summary>
internal readonly record struct ImageReference(Registry? Registry, string Repository, string Tag) {
    public override string ToString()
    {
        if (Registry is {} reg) {
            return $"{reg.BaseUri.GetComponents(UriComponents.HostAndPort, UriFormat.Unescaped)}/{Repository}:{Tag}";
        } else {
            return RepositoryAndTag;
        }
    }

    /// <summary>
    /// Returns the repository and tag as a formatted string. Used in cases
    /// </summary>
    public readonly string RepositoryAndTag => $"{Repository}:{Tag}";
}
