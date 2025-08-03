// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Structured access to the content store for manifests and blobs at a given root path.
/// </summary>
/// <param name="root"></param>
public class ContentStore(DirectoryInfo root)
{
    public string ArtifactRoot
    {
        get
        {
            string artifactPath = Path.Join(root.FullName, "Containers");
            Directory.CreateDirectory(artifactPath);
            return artifactPath;
        }
    }

    /// <summary>
    /// Where all the blobs are stored in this ContentStore - these will be addressed purely by digest. The contents may be JSON blobs,
    /// layer tarballs, or other - you need to know the media type to interpret the contents.
    /// </summary>
    public string ContentRoot
    {
        get
        {
            string contentPath = Path.Join(ArtifactRoot, "Content");
            Directory.CreateDirectory(contentPath);
            return contentPath;
        }
    }

    /// <summary>
    /// Where all the reference pointers are stored in this ContentStore. These will be addressed by logical reference - registry, repository, tag.
    /// The contents will be a digest and media type, which can then be looked up in the <see cref="ContentRoot"/>.
    /// </summary>
    public string ReferenceRoot
    {
        get
        {
            string referencePath = Path.Combine(ArtifactRoot, "Manifests");
            Directory.CreateDirectory(referencePath);
            return referencePath;
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

    /// <summary>
    /// A safety valve on top of <see cref="GetPathForHash"/> that also validates that we know/understand the media type of the Descriptor
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">If the Descriptor isn't a layer mediatype</exception>
    public string PathForDescriptor(Descriptor descriptor)
    {
        string extension = descriptor.MediaType switch
        {
            "application/vnd.docker.image.rootfs.diff.tar.gzip"
            or "application/vnd.oci.image.layer.v1.tar+gzip"
            or "application/vnd.docker.image.rootfs.foreign.diff.tar.gzip"
                => ".tar.gz",
            "application/vnd.docker.image.rootfs.diff.tar"
            or "application/vnd.oci.image.layer.v1.tar"
                => ".tar",
            SchemaTypes.DockerManifestListV2
            or SchemaTypes.DockerManifestV2
            or SchemaTypes.OciImageIndexV1
            or SchemaTypes.OciManifestV1
            or SchemaTypes.DockerContainerV1
            or SchemaTypes.OciImageConfigV1 => string.Empty,
            _ => throw new ArgumentException(Resource.FormatString(nameof(Strings.UnrecognizedMediaType), descriptor.MediaType))
        };

        return GetPathForHash(descriptor.Digest.Value) + extension;
    }

    /// <summary>
    /// Returns the path in the <see cref="ReferenceRoot"/> for the manifest reference for this registry/repository/tag.
    /// </summary>
    public string PathForManifestByTag(string registry, string repository, string tag) => Path.Combine(ReferenceRoot, SafeFileName(registry), repository, tag);

    /// <summary>
    /// Returns the path in the <see cref="ReferenceRoot"/> for the manifest reference for this digest.
    /// </summary>
    public string PathForManifestByDigest(string registry, string repository, Digest digest) => Path.Combine(ReferenceRoot, SafeFileName(registry), repository, digest.ToString());

    private static string SafeFileName(string s) =>
        Path.GetInvalidPathChars()
        .Aggregate(s, (current, c) => current.Replace(c.ToString(), "_"))
        .Replace(':', '_'); // ':' is not a valid path character, but we use it in digests

    /// <summary>
    /// Returns the path to the content store for a given content hash (<c>algo</c>:<c>digest</c>) pair.
    /// </summary>
    /// <param name="contentHash"></param>
    /// <returns></returns>
    public string GetPathForHash(string contentHash) => Path.Combine(ContentRoot, contentHash);

    public string GetTempFile() => Path.Join(TempPath, Path.GetRandomFileName());
}
