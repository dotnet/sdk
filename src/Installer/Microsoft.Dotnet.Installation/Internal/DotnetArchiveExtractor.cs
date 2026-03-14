// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Formats.Tar;
using System.IO.Compression;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

internal class DotnetArchiveExtractor : IDisposable
{
    private readonly DotnetInstallRequest _request;
    private readonly ReleaseVersion _resolvedVersion;
    private readonly IProgressTarget? _progressTarget;
    private readonly IArchiveDownloader _archiveDownloader;
    private readonly bool _shouldDisposeDownloader;
    private readonly bool _ownsProgressReporter = true;
    private MuxerHandler? MuxerHandler { get; set; }
    private string? _archivePath;
    private IProgressReporter? _progressReporter;
    private readonly HashSet<string> _extractedSubcomponents = [];

    /// <summary>
    /// Gets the list of subcomponent identifiers that were extracted during the last Commit() call.
    /// </summary>
    public IReadOnlyList<string> ExtractedSubcomponents => [.. _extractedSubcomponents];

    public DotnetArchiveExtractor(
        DotnetInstallRequest request,
        ReleaseVersion resolvedVersion,
        ReleaseManifest releaseManifest,
        IProgressTarget progressTarget,
        IArchiveDownloader? archiveDownloader = null,
        string? cacheDirectory = null)
        : this(request, resolvedVersion, releaseManifest, archiveDownloader, cacheDirectory)
    {
        _progressTarget = progressTarget;
    }

    /// <summary>
    /// Constructor that accepts a shared IProgressReporter, allowing multiple extractors
    /// to display progress tasks within the same progress widget.
    /// </summary>
    public DotnetArchiveExtractor(
        DotnetInstallRequest request,
        ReleaseVersion resolvedVersion,
        ReleaseManifest releaseManifest,
        IProgressReporter sharedReporter,
        IArchiveDownloader? archiveDownloader = null,
        string? cacheDirectory = null)
        : this(request, resolvedVersion, releaseManifest, archiveDownloader, cacheDirectory)
    {
        _progressReporter = sharedReporter;
        _ownsProgressReporter = false;
    }

    private DotnetArchiveExtractor(
        DotnetInstallRequest request,
        ReleaseVersion resolvedVersion,
        ReleaseManifest releaseManifest,
        IArchiveDownloader? archiveDownloader,
        string? cacheDirectory = null)
    {
        _request = request;
        _resolvedVersion = resolvedVersion;
        ScratchDownloadDirectory = Directory.CreateTempSubdirectory().FullName;

        if (archiveDownloader != null)
        {
            _archiveDownloader = archiveDownloader;
            _shouldDisposeDownloader = false;
        }
        else
        {
            _archiveDownloader = new DotnetArchiveDownloader(releaseManifest, cacheDirectory: cacheDirectory);
            _shouldDisposeDownloader = true;
        }
    }

    /// <summary>
    /// Gets the scratch download directory path. Exposed for testing.
    /// </summary>
    internal string ScratchDownloadDirectory { get; }

    /// <summary>
    /// Gets or creates the shared progress reporter for both Prepare and Commit phases.
    /// This avoids multiple newlines from Spectre.Console Progress between phases.
    /// When a shared reporter was provided via the constructor, that instance is returned directly.
    /// </summary>
    private IProgressReporter ProgressReporter => _progressReporter ??= _progressTarget!.CreateProgressReporter();

    public void Prepare()
    {
        using var activity = InstallationActivitySource.ActivitySource.StartActivity("download");
        activity?.SetTag("download.version", _resolvedVersion.ToString());

        var archiveName = $"dotnet-{Guid.NewGuid()}";
        _archivePath = Path.Combine(ScratchDownloadDirectory, archiveName + DotnetupUtilities.GetArchiveFileExtensionForPlatform());

        string description = InstallComponentExtensions.FormatProgressDescription("Downloading", _request.Component, _resolvedVersion.ToString());
        var downloadTask = ProgressReporter.AddTask(description, 100);
        var reporter = new DownloadProgressReporter(downloadTask, description);

        try
        {
            _archiveDownloader.DownloadArchiveWithVerification(_request, _resolvedVersion, _archivePath, reporter);
        }
        catch (DotnetInstallException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.DownloadFailed,
                $"Failed to download .NET archive for version {_resolvedVersion}: {ex.Message}",
                ex,
                version: _resolvedVersion.ToString(),
                component: _request.Component.ToString());
        }
        catch (Exception ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.DownloadFailed,
                $"Failed to download .NET archive for version {_resolvedVersion}: {ex.Message}",
                ex,
                version: _resolvedVersion.ToString(),
                component: _request.Component.ToString());
        }

        downloadTask.Value = 100;
    }

    public void Commit()
    {
        using var activity = InstallationActivitySource.ActivitySource.StartActivity("extract");
        activity?.SetTag("download.version", _resolvedVersion.ToString());

        _extractedSubcomponents.Clear();

        string description = InstallComponentExtensions.FormatProgressDescription("Installing", _request.Component, _resolvedVersion.ToString());
        var installTask = ProgressReporter.AddTask(description, maxValue: 100);

        if (_archivePath is null)
        {
            throw new InvalidOperationException("Prepare() must be called before Commit().");
        }

        ExtractWithExceptionHandling(_archivePath, _request.InstallRoot.Path!, installTask);
    }

    private void ExtractWithExceptionHandling(string archivePath, string targetPath, IProgressTask installTask)
    {
        try
        {
            ExtractArchiveDirectlyToTarget(archivePath, targetPath, installTask);
            installTask.Value = installTask.MaxValue;
        }
        catch (DotnetInstallException)
        {
            throw;
        }
        catch (InvalidDataException ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ArchiveCorrupted,
                $"Archive is corrupted or truncated for version {_resolvedVersion}: {ex.Message}",
                ex,
                version: _resolvedVersion.ToString(),
                component: _request.Component.ToString());
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.PermissionDenied,
                $"Permission denied while extracting .NET archive for version {_resolvedVersion}: {ex.Message}",
                ex,
                version: _resolvedVersion.ToString(),
                component: _request.Component.ToString());
        }
        catch (IOException ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ExtractionFailed,
                $"Failed to extract .NET archive for version {_resolvedVersion}: {ex.Message}",
                ex,
                version: _resolvedVersion.ToString(),
                component: _request.Component.ToString());
        }
        catch (Exception ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ExtractionFailed,
                $"Failed to extract .NET archive for version {_resolvedVersion}: {ex.Message}",
                ex,
                version: _resolvedVersion.ToString(),
                component: _request.Component.ToString());
        }
    }

    /// <summary>
    /// Extracts the archive directly to the target directory with special handling for muxer.
    /// Combines extraction and installation into a single operation.
    /// </summary>
    private void ExtractArchiveDirectlyToTarget(string archivePath, string targetDir, IProgressTask? installTask)
    {
        Directory.CreateDirectory(targetDir);

        // Capture pre-extraction muxer/runtime state right before extraction so
        // the snapshot is as accurate as possible (caller holds the mutex here).
        if (MuxerHandler is null && _request.InstallRoot.Path is not null)
        {
            MuxerHandler = new MuxerHandler(_request.InstallRoot.Path, _request.Options.RequireMuxerUpdate);
        }

        // Build a predicate that skips entries whose subcomponent already exists on disk.
        // The archive is still downloaded and all subcomponents are tracked for the manifest,
        // but extraction is skipped to avoid overwriting files from an earlier installation.
        var shouldSkipEntry = CreateExistingSubcomponentSkipPredicate(targetDir);

        // Extract archive, redirecting muxer to temp path and skipping existing subcomponents
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ExtractTarArchive(archivePath, targetDir, installTask, MuxerHandler, TrackSubcomponent, shouldSkipEntry);
        }
        else
        {
            ExtractZipArchive(archivePath, targetDir, installTask, MuxerHandler, TrackSubcomponent, shouldSkipEntry);
        }

        // After extraction, decide whether to keep or discard the temp muxer
        MuxerHandler?.FinalizeAfterExtraction();
    }

    /// <summary>
    /// Creates a predicate that returns true for archive entries whose subcomponent
    /// directory already exists on disk. Used to skip re-extracting subcomponents
    /// that were installed by a previous installation (e.g., a runtime that overlaps
    /// with an already-installed SDK). The entry is still reported to the
    /// <c>onEntryExtracted</c> callback so the subcomponent is recorded in the manifest.
    /// </summary>
    private static Func<string, bool> CreateExistingSubcomponentSkipPredicate(string targetDir)
    {
        var cache = new Dictionary<string, bool>(StringComparer.Ordinal);

        return (string entryName) =>
        {
            var subcomponentId = SubcomponentResolver.Resolve(entryName);
            if (subcomponentId is null)
            {
                return false;
            }

            if (!cache.TryGetValue(subcomponentId, out bool exists))
            {
                var subcomponentPath = Path.Combine(targetDir, subcomponentId.Replace('/', Path.DirectorySeparatorChar));
                exists = Directory.Exists(subcomponentPath);
                cache[subcomponentId] = exists;

                if (exists)
                {
                    System.Diagnostics.Debug.WriteLine($"Subcomponent '{subcomponentId}' already exists on disk, skipping extraction.");
                }
            }

            return exists;
        };
    }

    /// <summary>
    /// Resolves the destination path for an archive entry, redirecting the muxer to a temp path if needed.
    /// </summary>
    /// <param name="entryName">The entry name/path from the archive.</param>
    /// <param name="targetDir">The target extraction directory.</param>
    /// <param name="muxerHandler">Optional muxer handler for redirecting muxer entries.</param>
    /// <returns>The resolved destination path.</returns>
    private static string ResolveEntryDestPath(string entryName, string targetDir, MuxerHandler? muxerHandler)
    {
        // Normalize entry name by stripping leading "./" prefix (common in tar archives)
        string normalizedName = entryName.StartsWith("./", StringComparison.Ordinal)
            ? entryName.Substring(2)
            : entryName;

        if (muxerHandler != null && normalizedName == MuxerHandler.MuxerEntryName)
        {
            muxerHandler.MuxerWasExtracted = true;
            return muxerHandler.TempMuxerPath;
        }

        string destPath = Path.GetFullPath(Path.Combine(targetDir, normalizedName));
        string fullTargetDir = Path.GetFullPath(targetDir) + Path.DirectorySeparatorChar;
        if (!destPath.StartsWith(fullTargetDir, DotnetupUtilities.PathComparison) &&
            !string.Equals(destPath, Path.GetFullPath(targetDir), DotnetupUtilities.PathComparison))
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.ArchiveCorrupted,
                $"Archive entry '{entryName}' would extract outside target directory.");
        }

        return destPath;
    }

    /// <summary>
    /// Initializes progress tracking for extraction by setting the max value.
    /// </summary>
    private static void InitializeExtractionProgress(IProgressTask? installTask, long totalEntries)
    {
        installTask?.MaxValue = totalEntries > 0 ? totalEntries : 1;
    }

    /// <summary>
    /// Extracts a tar or tar.gz archive to the target directory.
    /// </summary>
    private static void ExtractTarArchive(string archivePath, string targetDir, IProgressTask? installTask, MuxerHandler? muxerHandler = null, Action<string>? onEntryExtracted = null, Func<string, bool>? shouldSkipEntry = null)
    {
        string decompressedPath = DecompressTarGzIfNeeded(archivePath, out bool needsDecompression);

        try
        {
            InitializeExtractionProgress(installTask, CountTarEntries(decompressedPath));
            ExtractTarContents(decompressedPath, targetDir, installTask, muxerHandler, onEntryExtracted, shouldSkipEntry);
        }
        finally
        {
            // Clean up temporary decompressed file
            if (needsDecompression && File.Exists(decompressedPath))
            {
                File.Delete(decompressedPath);
            }
        }
    }

    /// <summary>
    /// Decompresses a .tar.gz file if needed, returning the path to the tar file.
    /// </summary>
    private static string DecompressTarGzIfNeeded(string archivePath, out bool needsDecompression)
    {
        needsDecompression = archivePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
        if (!needsDecompression)
        {
            return archivePath;
        }

        string decompressedPath = archivePath.Replace(".gz", "");

        using FileStream originalFileStream = File.OpenRead(archivePath);
        using FileStream decompressedFileStream = File.Create(decompressedPath);
        using GZipStream decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress);
        decompressionStream.CopyTo(decompressedFileStream);

        return decompressedPath;
    }

    /// <summary>
    /// Counts the number of entries in a tar file for progress reporting.
    /// </summary>
    private static long CountTarEntries(string tarPath)
    {
        long totalFiles = 0;
        using var tarStream = File.OpenRead(tarPath);
        var tarReader = new TarReader(tarStream);
        while (tarReader.GetNextEntry() is not null)
        {
            totalFiles++;
        }
        return totalFiles;
    }

    /// <summary>
    /// Extracts the contents of a tar file to the target directory.
    /// Exposed as internal static for testing.
    /// </summary>
    internal static void ExtractTarContents(string tarPath, string targetDir, IProgressTask? installTask, MuxerHandler? muxerHandler = null, Action<string>? onEntryExtracted = null, Func<string, bool>? shouldSkipEntry = null)
    {
        using var tarStream = File.OpenRead(tarPath);
        var tarReader = new TarReader(tarStream);
        TarEntry? entry;

        var deferredHardLinks = new List<(string DestPath, string TargetPath)>();

        while ((entry = tarReader.GetNextEntry()) is not null)
        {
            bool skip = shouldSkipEntry?.Invoke(entry.Name) ?? false;
            if (!skip)
            {
                ProcessTarEntry(entry, targetDir, muxerHandler, deferredHardLinks);
            }

            onEntryExtracted?.Invoke(entry.Name);
            installTask?.Value += 1;
        }

        CreateDeferredHardLinks(deferredHardLinks);
    }

    private static void ProcessTarEntry(TarEntry entry, string targetDir, MuxerHandler? muxerHandler, List<(string DestPath, string TargetPath)> deferredHardLinks)
    {
        if (entry.EntryType == TarEntryType.RegularFile)
        {
            string destPath = ResolveEntryDestPath(entry.Name, targetDir, muxerHandler);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            entry.ExtractToFile(destPath, overwrite: true);
        }
        else if (entry.EntryType == TarEntryType.Directory)
        {
            string dirPath = ResolveEntryDestPath(entry.Name, targetDir, muxerHandler);
            Directory.CreateDirectory(dirPath);

            if (entry.Mode != default && !OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(dirPath, entry.Mode);
            }
        }
        else if (entry.EntryType == TarEntryType.SymbolicLink)
        {
            string destPath = ResolveEntryDestPath(entry.Name, targetDir, muxerHandler);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            if (File.Exists(destPath) || Directory.Exists(destPath))
            {
                File.Delete(destPath);
            }

            File.CreateSymbolicLink(destPath, entry.LinkName!);
        }
        else if (entry.EntryType == TarEntryType.HardLink)
        {
            string destPath = ResolveEntryDestPath(entry.Name, targetDir, muxerHandler);
            string linkTargetPath = ResolveEntryDestPath(entry.LinkName!, targetDir, muxerHandler);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            deferredHardLinks.Add((destPath, linkTargetPath));
        }
    }

    private static void CreateDeferredHardLinks(List<(string DestPath, string TargetPath)> deferredHardLinks)
    {
        foreach (var (destPath, targetPath) in deferredHardLinks)
        {
            if (File.Exists(destPath))
            {
                File.Delete(destPath);
            }

            File.CreateHardLink(destPath, targetPath);
        }
    }

    /// <summary>
    /// Extracts a zip archive to the target directory.
    /// </summary>
    private static void ExtractZipArchive(string archivePath, string targetDir, IProgressTask? installTask, MuxerHandler? muxerHandler = null, Action<string>? onEntryExtracted = null, Func<string, bool>? shouldSkipEntry = null)
    {
        using var zip = ZipFile.OpenRead(archivePath);
        InitializeExtractionProgress(installTask, zip.Entries.Count);

        foreach (var entry in zip.Entries)
        {
            bool skip = shouldSkipEntry?.Invoke(entry.FullName) ?? false;

            if (!skip)
            {
                // Directory entries have no file name
                if (string.IsNullOrEmpty(Path.GetFileName(entry.FullName)))
                {
                    Directory.CreateDirectory(Path.Combine(targetDir, entry.FullName));
                }
                else
                {
                    string destPath = ResolveEntryDestPath(entry.FullName, targetDir, muxerHandler);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    entry.ExtractToFile(destPath, overwrite: true);
                }
            }

            onEntryExtracted?.Invoke(entry.FullName);
            installTask?.Value += 1;
        }
    }

    private void TrackSubcomponent(string relativeEntryPath)
    {
        var subcomponent = SubcomponentResolver.Resolve(relativeEntryPath, out var resolveResult);
        if (subcomponent is not null)
        {
            _extractedSubcomponents.Add(subcomponent);
            return;
        }

        switch (resolveResult)
        {
            case SubcomponentResolveResult.UnknownFolder:
                Console.Error.WriteLine($"Warning: Unrecognized subcomponent path '{relativeEntryPath}' in archive. This file will not be tracked by dotnetup.");
                break;
            case SubcomponentResolveResult.TooShallow:
                Console.Error.WriteLine($"Warning: File '{relativeEntryPath}' is in a known folder but not deep enough to be tracked as a subcomponent.");
                break;
        }
    }

    public void Dispose()
    {
        try
        {
            // Dispose the progress reporter only if we own it (not shared)
            if (_ownsProgressReporter)
            {
                _progressReporter?.Dispose();
            }
        }
        catch
        {
        }

        try
        {
            // Dispose the archive downloader if we created it
            if (_shouldDisposeDownloader)
            {
                _archiveDownloader.Dispose();
            }
        }
        catch
        {
        }

        try
        {
            // Clean up temporary download directory
            if (Directory.Exists(ScratchDownloadDirectory))
            {
                Directory.Delete(ScratchDownloadDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }
}
