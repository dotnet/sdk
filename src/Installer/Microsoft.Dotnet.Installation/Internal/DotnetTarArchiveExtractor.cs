// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Formats.Tar;
using System.IO.Compression;

namespace Microsoft.Dotnet.Installation.Internal;

internal sealed class DotnetTarArchiveExtractor : ITarArchiveExtractor
{
    public void Extract(TarExtractionContext context)
    {
        bool isGzip = context.ArchivePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);

        // Keep the archive open throughout extraction so cleanup cannot remove it between
        // the optional counting pass and the extraction pass.
        using var archiveStream = new FileStream(context.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        IProgressTask? entryProgressTask = context.ProgressTask?.RequiresKnownMaximum == true
            ? context.ProgressTask
            : null;
        if (entryProgressTask is not null)
        {
            long totalEntries = CountEntries(archiveStream, isGzip);
            entryProgressTask.MaxValue = totalEntries > 0 ? totalEntries : 1;
        }

        archiveStream.Seek(0, SeekOrigin.Begin);
        ExtractContents(archiveStream, isGzip, context, entryProgressTask);
    }

    private static long CountEntries(Stream archiveStream, bool isGzip)
    {
        long totalFiles = 0;
        using OwnedTarStream tarStream = OpenReadStream(archiveStream, isGzip);
        using var tarReader = new TarReader(tarStream.Stream, leaveOpen: true);
        while (tarReader.GetNextEntry() is not null)
        {
            totalFiles++;
        }

        return totalFiles;
    }

    /// <summary>
    /// Wraps an already-open TAR stream for reading and adds gzip decompression when needed.
    /// Disposing the returned wrapper never closes <paramref name="archiveStream"/>.
    /// </summary>
    private static OwnedTarStream OpenReadStream(Stream archiveStream, bool isGzip)
        => isGzip
            ? new OwnedTarStream(new GZipStream(archiveStream, CompressionMode.Decompress, leaveOpen: true))
            : new OwnedTarStream(archiveStream, ownsStream: false);

    private static void ExtractContents(
        Stream archiveStream,
        bool isGzip,
        TarExtractionContext context,
        IProgressTask? entryProgressTask)
    {
        using OwnedTarStream tarStream = OpenReadStream(archiveStream, isGzip);
        using var tarReader = new TarReader(tarStream.Stream, leaveOpen: true);

        // A hardlink target can appear later in the archive, so create hardlinks only after
        // all regular files have been extracted.
        var deferredHardLinks = new List<(string DestinationPath, string TargetPath)>();
        while (tarReader.GetNextEntry() is { } entry)
        {
            bool skip = context.ShouldSkipEntry?.Invoke(entry.Name) ?? false;
            if (!skip)
            {
                ProcessEntry(entry, context.TargetDirectory, context.MuxerHandler, deferredHardLinks);
            }

            context.OnEntryExtracted?.Invoke(entry.Name);
            entryProgressTask?.Value += 1;
        }

        CreateDeferredHardLinks(deferredHardLinks);
    }

    private static void ProcessEntry(
        TarEntry entry,
        string targetDirectory,
        MuxerHandler? muxerHandler,
        List<(string DestinationPath, string TargetPath)> deferredHardLinks)
    {
        string destinationPath;
        switch (entry.EntryType)
        {
            case TarEntryType.RegularFile:
                destinationPath = ArchiveEntryPathResolver.ResolveDestination(entry.Name, targetDirectory, muxerHandler);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                entry.ExtractToFile(destinationPath, overwrite: true);
                break;
            case TarEntryType.Directory:
                destinationPath = ArchiveEntryPathResolver.ResolveDestination(entry.Name, targetDirectory, muxerHandler);
                Directory.CreateDirectory(destinationPath);
                if (entry.Mode != default && !OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(destinationPath, entry.Mode);
                }
                break;
            case TarEntryType.SymbolicLink:
                destinationPath = ArchiveEntryPathResolver.ResolveDestination(entry.Name, targetDirectory, muxerHandler);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                if (File.Exists(destinationPath) || Directory.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                File.CreateSymbolicLink(destinationPath, entry.LinkName!);
                break;
            case TarEntryType.HardLink:
                destinationPath = ArchiveEntryPathResolver.ResolveDestination(entry.Name, targetDirectory, muxerHandler);
                string targetPath = ArchiveEntryPathResolver.ResolveDestination(entry.LinkName!, targetDirectory, muxerHandler);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                deferredHardLinks.Add((destinationPath, targetPath));
                break;
            default:
                Console.Error.WriteLine($"Warning: Skipping unsupported tar entry type '{entry.EntryType}' for '{entry.Name}'.");
                break;
        }
    }

    private static void CreateDeferredHardLinks(List<(string DestinationPath, string TargetPath)> deferredHardLinks)
    {
        foreach (var (destinationPath, targetPath) in deferredHardLinks)
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            File.CreateHardLink(destinationPath, targetPath);
        }
    }
}
