// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.Tasks;

/// <summary>
/// An MSBuild Task whose job is to take a list of input files and some metadata about them, and create a single container image layer containing them.
/// Since this is a tarball, on .NET Framework we use a ToolTask to shell out to a .NET tool to create the tarball. 
/// </summary>
partial class CreateAppLayerTask
{
    /// <summary>
    /// The files to pack into the layer. Each file must have a RelativePath metadata item set, which will be used to determine the path of the file within the container.
    /// </summary>
    [Required]
    public ITaskItem[] PublishFiles { get; set; } = [];

    /// <summary>
    /// The directory within the container to place the files. Each files' RelativePath will be relative to this directory.
    /// </summary>
    [Required]
    public string ContainerRootDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Used to determine what kind of layer to create. If this is a Windows RID, a Windows layer will be created; otherwise, a Linux layer will be created. 
    /// </summary>
    [Required]
    public string TargetRuntimeIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// The media type of the layer to create. This is used to determine how the layer will be created and what format it will be in.
    /// </summary>
    [Required]
    public string LayerMediaType { get; set; } = string.Empty;

    /// <summary>
    /// The path to the local storage location where the created layer will be stored for anything 'downstream' that looks up objects by digest.
    /// The layer will _also_ be written to the path specified by GeneratedLayerPath, but this is the location where it will be stored for later retrieval.
    /// </summary>
    [Required]
    public string ContentStoreRoot { get; set; } = string.Empty;

    /// <summary>
    /// The path to which the layer will be written. This is the final output of this task, and is the location where the layer can be found after the task completes.
    /// This is not an Output because it needs to be precomputed for incrementality purposes.
    /// </summary>
    [Required]
    public string GeneratedLayerPath { get; set; } = string.Empty;

    /// <summary>
    /// The output layer that was created by this task. This is the layer that can be used by other tasks or tools to create a container image.
    /// This will have Size, MediaType, and Digest metadata set on it, which can be used to determine the properties of the layer by other parts of the build process.
    /// </summary>
    [Output]
    public ITaskItem GeneratedAppContainerLayer { get; set; } = null!;

    public CreateAppLayerTask()
    {

        TaskResources = Resource.Manager;
    }
}
