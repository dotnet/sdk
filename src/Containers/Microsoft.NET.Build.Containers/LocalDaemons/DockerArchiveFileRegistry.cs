// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.LocalDaemons;

internal class DockerArchiveFileRegistry : ArchiveFileRegistry
{

    public DockerArchiveFileRegistry(string archiveOutputPath) : base(archiveOutputPath, ContainerImageArchiveFormat.Docker)
    {
    }

    protected override Task WriteImageToStreamAsync(BuiltImage image, SourceImageReference sourceReference,
        DestinationImageReference destinationReference, Stream imageStream, CancellationToken cancellationToken)
    {
        return DockerCli.WriteImageToStreamAsync(
            image,
            sourceReference,
            destinationReference,
            imageStream,
            cancellationToken);
    }
}
