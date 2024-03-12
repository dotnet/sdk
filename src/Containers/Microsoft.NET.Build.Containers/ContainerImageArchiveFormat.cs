// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Lists all supported container image archive formats.
/// </summary>
public enum ContainerImageArchiveFormat
{
    /// <summary>
    /// The Docker Image Archive format.
    /// https://github.com/moby/moby/blob/master/image/spec/spec.md
    /// </summary>
    Docker,
    /// <summary>
    /// The Open Container Initiative (OCI) archive format.
    /// https://github.com/opencontainers/image-spec/blob/main/image-layout.md
    /// </summary>
    OpenContainerInitiative
}
