// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.Tasks;

partial class CreateImageIndex
{
    /// <summary>
    /// The base registry to pull from.
    /// Ex: mcr.microsoft.com
    /// </summary>
    public string BaseRegistry { get; set; }

    /// <summary>
    /// The base image to pull.
    /// Ex: dotnet/runtime
    /// </summary>
    [Required]
    public string BaseImageName { get; set; }

    /// <summary>
    /// The base image tag.
    /// Ex: 6.0
    /// </summary>
    public string BaseImageTag { get; set; }

    /// <summary>
    /// The base image digest.
    /// Ex: sha256:12345...
    /// </summary>
    public string BaseImageDigest { get; set; }

    /// <summary>
    /// Manifests to include in the image index.
    /// </summary>
    [Required]
    public ITaskItem[] GeneratedContainers { get; set; }

    /// <summary>
    /// The registry to push the image index to.
    /// </summary>
    public string OutputRegistry { get; set; }

    /// <summary>
    /// The file path to which to write a tar.gz archive of the container image.
    /// </summary>
    public string ArchiveOutputPath { get; set; }

    /// <summary>
    /// The kind of local registry to use, if any.
    /// </summary>
    public string LocalRegistry { get; set; }

    /// <summary>
    /// The name of the output image index (manifest list) that will be pushed to the registry.
    /// </summary>
    [Required]
    public string Repository { get; set; }

    /// <summary>
    /// The tag to associate with the new image index (manifest list).
    /// </summary>
    [Required]
    public string[] ImageTags { get; set; }

    /// <summary>
    /// The generated archive output path.
    /// </summary>
    [Output]
    public string GeneratedArchiveOutputPath { get; set; }

    /// <summary>
    /// The generated image index (manifest list) in JSON format.
    /// </summary>
    [Output]
    public string GeneratedImageIndex { get; set; }

    public CreateImageIndex()
    {
        BaseRegistry = string.Empty;
        BaseImageName = string.Empty;
        BaseImageTag = string.Empty;
        BaseImageDigest = string.Empty;
        GeneratedContainers = Array.Empty<ITaskItem>();
        OutputRegistry = string.Empty;
        ArchiveOutputPath = string.Empty;
        LocalRegistry = string.Empty;
        Repository = string.Empty;
        ImageTags = Array.Empty<string>();
        GeneratedArchiveOutputPath = string.Empty;
        GeneratedImageIndex = string.Empty;

        TaskResources = Resource.Manager;
    }
} 