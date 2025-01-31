// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.LocalDaemons;

internal class ArchiveFileRegistry : ILocalRegistry
{
    public string ArchiveOutputPath { get; private set; }

    public ArchiveFileRegistry(string archiveOutputPath)
    {
        ArchiveOutputPath = archiveOutputPath;
    }

    private async Task LoadAsync<T>(T image, SourceImageReference sourceReference, 
        DestinationImageReference destinationReference, CancellationToken cancellationToken,
        Func<T, SourceImageReference, DestinationImageReference, Stream, CancellationToken, Task> writeStreamFunc)
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

        // Call the delegate to write the image to the stream
        await writeStreamFunc(image, sourceReference, destinationReference, fileStream, cancellationToken).ConfigureAwait(false);
    }

    public async Task LoadAsync(BuiltImage image, SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        CancellationToken cancellationToken) 
        => await LoadAsync(image, sourceReference, destinationReference, cancellationToken,
            DockerCli.WriteImageToStreamAsync);

    public async Task LoadAsync(BuiltImage[] images, SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        CancellationToken cancellationToken) 
        => await LoadAsync(images, sourceReference, destinationReference, cancellationToken,
            DockerCli.WriteMultiArchOciImageToStreamAsync);

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public bool IsAvailable() => true;

    public override string ToString()
    {
        return string.Format(Strings.ArchiveRegistry_PushInfo, ArchiveOutputPath);
    }
}
