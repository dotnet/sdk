// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Structured access to the content store for manifests and blobs at a given root path.
/// </summary>
/// <param name="root"></param>
internal class ContentStore(DirectoryInfo root)
{
    public string ArtifactRoot
    {
        get
        {
            Directory.CreateDirectory(root.FullName);
            return root.FullName;
        }
    }
    
    public string ContentRoot
    {
        get
        {
            string contentPath = Path.Join(ArtifactRoot, "Content");
            Directory.CreateDirectory(contentPath);
            return contentPath;
        }
    }

    public string TempPath
    {
        get
        {
            string tempPath = Path.Join(ArtifactRoot, "Temp");
            Directory.CreateDirectory(tempPath);
            return tempPath;
        }
    }

    public string PathForDescriptor(Descriptor descriptor)
    {
        string digest = descriptor.Digest;

        Debug.Assert(digest.StartsWith("sha256:", StringComparison.Ordinal));

        string contentHash = digest.Substring("sha256:".Length);

        string extension = descriptor.MediaType switch
        {
            "application/vnd.docker.image.rootfs.diff.tar.gzip"
            or "application/vnd.oci.image.layer.v1.tar+gzip"
            or "application/vnd.docker.image.rootfs.foreign.diff.tar.gzip"
                => ".tar.gz",
            "application/vnd.docker.image.rootfs.diff.tar"
            or "application/vnd.oci.image.layer.v1.tar"
                => ".tar",
            _ => throw new ArgumentException(Resource.FormatString(nameof(Strings.UnrecognizedMediaType), descriptor.MediaType))
        };

        return GetPathForHash(contentHash) + extension;
    }

    /// <summary>
    /// Returns the path to the content store for a given content hash (<c>algo</c>:<c>digest</c>) pair.
    /// </summary>
    /// <param name="contentHash"></param>
    /// <returns></returns>
    public string GetPathForHash(string contentHash) => Path.Combine(ContentRoot, contentHash);

    public string GetTempFile() => Path.Join(TempPath, Path.GetRandomFileName());
}
