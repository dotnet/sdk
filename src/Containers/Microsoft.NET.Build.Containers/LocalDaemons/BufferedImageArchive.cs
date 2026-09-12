// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Buffers a streamed image archive in memory and spills it to disk after a bounded threshold.
/// </summary>
internal sealed class BufferedImageArchive : IAsyncDisposable
{
    internal const int DefaultMemoryThreshold = 512 * 1024 * 1024;

    private readonly DirectoryInfo? _spoolDirectory;

    private BufferedImageArchive(Stream content, DirectoryInfo? spoolDirectory)
    {
        Content = content;
        _spoolDirectory = spoolDirectory;
    }

    internal Stream Content { get; }

    internal bool IsFileBacked => _spoolDirectory is not null;

    internal static Task<BufferedImageArchive> CreateAsync(
        Stream source,
        CancellationToken cancellationToken)
        => CreateAsync(source, DefaultMemoryThreshold, cancellationToken);

    internal static async Task<BufferedImageArchive> CreateAsync(
        Stream source,
        int memoryThreshold,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(memoryThreshold);

        Stream destination = new MemoryStream();
        DirectoryInfo? spoolDirectory = null;
        try
        {
            byte[] buffer = new byte[128 * 1024];
            long totalBytes = 0;
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
            {
                if (destination is MemoryStream memory && totalBytes + bytesRead > memoryThreshold)
                {
                    spoolDirectory = Directory.CreateTempSubdirectory("dotnet-local-base-export-");
                    FileStream file = File.Create(Path.Combine(spoolDirectory.FullName, "image.tar"));
                    destination = file;
                    memory.Position = 0;
                    try
                    {
                        await memory.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        await memory.DisposeAsync().ConfigureAwait(false);
                    }
                }

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                totalBytes += bytesRead;
            }

            destination.Position = 0;
            return new BufferedImageArchive(destination, spoolDirectory);
        }
        catch
        {
            await destination.DisposeAsync().ConfigureAwait(false);
            spoolDirectory?.Delete(recursive: true);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Content.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _spoolDirectory?.Delete(recursive: true);
        }
    }
}
