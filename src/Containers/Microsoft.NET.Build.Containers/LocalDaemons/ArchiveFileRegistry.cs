// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.LocalDaemons;

internal abstract class ArchiveFileRegistry : ILocalRegistry
{
    public string ArchiveOutputPath { get; private set; }
    public ContainerImageArchiveFormat ArchiveOutputFormat { get; }

    protected ArchiveFileRegistry(string archiveOutputPath, ContainerImageArchiveFormat archiveOutputFormat)
    {
        ArchiveOutputPath = archiveOutputPath;
        ArchiveOutputFormat = archiveOutputFormat;
    }

    public async Task LoadAsync(BuiltImage image, SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(ArchiveOutputPath);

        // pointing to a directory? -> append default name
        if (Directory.Exists(fullPath) || ArchiveOutputPath.EndsWith("/") || ArchiveOutputPath.EndsWith("\\"))
        {
            fullPath = Path.Combine(fullPath, destinationReference.Repository + ".tar.gz");
        }

        // create parent directory if required.
        var parentDirectory = Path.GetDirectoryName(fullPath);
        if (parentDirectory != null && !Directory.Exists(parentDirectory))
        {
            Directory.CreateDirectory(parentDirectory);
        }

        ArchiveOutputPath = fullPath;
        await using var fileStream = File.Create(fullPath);
        await WriteImageToStreamAsync(
            image,
            sourceReference,
            destinationReference,
            fileStream,
            cancellationToken).ConfigureAwait(false);
    }

    protected abstract Task WriteImageToStreamAsync(BuiltImage image, SourceImageReference sourceReference,
        DestinationImageReference destinationReference, Stream imageStream, CancellationToken cancellationToken);

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public bool IsAvailable() => true;


    public sealed override string ToString()
    {
        return string.Format(Strings.ArchiveRegistry_PushInfo, ArchiveOutputPath, ArchiveOutputFormat);
    }
}
