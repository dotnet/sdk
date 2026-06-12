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
    private readonly bool _ownsProgressReporter = true;
    private readonly int _versionDisplayWidth;
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
        : this(request, resolvedVersion, releaseManifest, archiveDownloader, cacheDirectory, versionDisplayWidth: resolvedVersion.ToString().Length)
    {
        _progressTarget = progressTarget;
    }

    /// <summary>
    /// Constructor for batched installs. The supplied <see cref="InstallBatchContext"/> carries
    /// the shared progress reporter (so multiple extractors render tasks in the same widget) and
    /// the batch's version-display width (so progress rows align across differing version lengths).
    /// </summary>
    public DotnetArchiveExtractor(
        DotnetInstallRequest request,
        ReleaseVersion resolvedVersion,
        ReleaseManifest releaseManifest,
        InstallBatchContext batchContext,
        IArchiveDownloader? archiveDownloader = null,
        string? cacheDirectory = null)
        : this(request, resolvedVersion, releaseManifest, archiveDownloader, cacheDirectory, batchContext.VersionDisplayWidth)
    {
        _progressReporter = batchContext.Reporter;
        _ownsProgressReporter = false;
    }

    private DotnetArchiveExtractor(
        DotnetInstallRequest request,
        ReleaseVersion resolvedVersion,
        ReleaseManifest releaseManifest,
        IArchiveDownloader? archiveDownloader,
        string? cacheDirectory,
        int versionDisplayWidth)
    {
        _request = request;
        _resolvedVersion = resolvedVersion;
        _versionDisplayWidth = versionDisplayWidth;
        ScratchDownloadDirectory = Directory.CreateTempSubdirectory().FullName;

        if (archiveDownloader != null)
        {
            _archiveDownloader = archiveDownloader;
        }
        else
        {
            _archiveDownloader = new DotnetArchiveDownloader(releaseManifest, cacheDirectory: cacheDirectory);
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

    private ExtractorProgressTracker ProgressTracker { get => field ??= new ExtractorProgressTracker(ProgressReporter, _request.Component, _resolvedVersion.ToString(), _versionDisplayWidth); }

    public void Prepare()
    {
        var archiveBaseName = $"dotnet-{Guid.NewGuid()}";
        var archiveBasePath = Path.Combine(ScratchDownloadDirectory, archiveBaseName);

        var (reporter, downloadTask) = ProgressTracker.BeginDownload();

        try
        {
            _archivePath = _archiveDownloader.DownloadArchiveWithVerification(_request, _resolvedVersion, archiveBasePath, reporter);
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

        ProgressTracker.CompleteDownload(downloadTask, _archivePath);
    }

    public void Commit()
    {
        using var op = Metrics.Track("extract/complete");
        op.Tag("download.version", _resolvedVersion.ToString());

        _extractedSubcomponents.Clear();

        var installTask = ProgressTracker.BeginExtraction();

        if (_archivePath is null)
        {
            throw new InvalidOperationException("Prepare() must be called before Commit().");
        }

        ExtractWithExceptionHandling(_archivePath, _request.InstallRoot.Path!, installTask);

        ProgressTracker.CompleteExtraction(installTask);
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
        var shouldSkipEntry = CreateExistingSubcomponentSkipPredicate(targetDir, _request.Options.Verbosity);

        // Extract archive, redirecting muxer to temp path and skipping existing subcomponents
        if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
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
    private static Func<string, bool> CreateExistingSubcomponentSkipPredicate(string targetDir, Verbosity verbosity)
    {
        var cache = new Dictionary<string, bool>(StringComparer.Ordinal);

        return entryName =>
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

                if (exists && verbosity >= Verbosity.Detailed)
                {
                    Console.Error.WriteLine($"Subcomponent '{subcomponentId}' already exists on disk, skipping extraction.");
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
    /// Extracts a tar or tar.gz archive to the target directory.
    /// </summary>
    private static void ExtractTarArchive(string archivePath, string targetDir, IProgressTask? installTask, MuxerHandler? muxerHandler = null, Action<string>? onEntryExtracted = null, Func<string, bool>? shouldSkipEntry = null)
    {
        bool isGzip = archivePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);

        // Hold a single read handle on the archive for the entire extraction. FileShare.Read
        // keeps the file locked against deletion on Windows, and on Unix an open handle keeps the
        // underlying data readable even if the path is unlinked. Either way the scratch directory
        // cannot be reaped out from under us (e.g. by CI temp cleanup) in the window between the
        // entry-count pass and the extraction pass. Both passes seek this shared stream back to the
        // start and wrap it in a fresh, non-owning GZipStream/TarReader so the handle stays open.
        using var archiveStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        long totalEntries = CountTarEntries(archiveStream, isGzip);
        installTask?.MaxValue = totalEntries > 0 ? totalEntries : 1;

        archiveStream.Seek(0, SeekOrigin.Begin);
        ExtractTarContents(archiveStream, isGzip, targetDir, installTask, muxerHandler, onEntryExtracted, shouldSkipEntry);
    }

    /// <summary>
    /// Wraps an already-open tar archive stream for reading, layering in gzip decompression when
    /// needed. The returned reader leaves <paramref name="archiveStream"/> open so a single file
    /// handle can be reused across multiple passes; callers dispose the returned stream only when
    /// it is a gzip wrapper (never the shared archive stream).
    /// </summary>
    private static Stream OpenTarReadStream(Stream archiveStream, bool isGzip)
        => isGzip ? new GZipStream(archiveStream, CompressionMode.Decompress, leaveOpen: true) : archiveStream;

    /// <summary>
    /// Counts the number of entries in a tar archive for progress reporting.
    /// </summary>
    private static long CountTarEntries(Stream archiveStream, bool isGzip)
    {
        long totalFiles = 0;
        Stream tarStream = OpenTarReadStream(archiveStream, isGzip);
        try
        {
            using var tarReader = new TarReader(tarStream, leaveOpen: true);
            while (tarReader.GetNextEntry() is not null)
            {
                totalFiles++;
            }
        }
        finally
        {
            // Only dispose the gzip wrapper; the shared archive stream stays open for the next pass.
            if (isGzip) { tarStream.Dispose(); }
        }
        return totalFiles;
    }

    /// <summary>
    /// Extracts the contents of a tar file to the target directory.
    /// Exposed as internal static for testing.
    /// </summary>
    internal static void ExtractTarContents(string tarPath, string targetDir, IProgressTask? installTask, MuxerHandler? muxerHandler = null, Action<string>? onEntryExtracted = null, Func<string, bool>? shouldSkipEntry = null)
    {
        bool isGzip = tarPath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
        using var archiveStream = new FileStream(tarPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        ExtractTarContents(archiveStream, isGzip, targetDir, installTask, muxerHandler, onEntryExtracted, shouldSkipEntry);
    }

    private static void ExtractTarContents(Stream archiveStream, bool isGzip, string targetDir, IProgressTask? installTask, MuxerHandler? muxerHandler, Action<string>? onEntryExtracted, Func<string, bool>? shouldSkipEntry)
    {
        Stream tarStream = OpenTarReadStream(archiveStream, isGzip);
        try
        {
            using var tarReader = new TarReader(tarStream, leaveOpen: true);
            TarEntry? entry;

            // Defer hard link creation until after all regular files are extracted,
            // since the target file may not exist yet when the hard link entry is encountered.
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
        finally
        {
            // Only dispose the gzip wrapper; the shared archive stream is owned by the caller.
            if (isGzip) { tarStream.Dispose(); }
        }
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
        else
        {
            Console.Error.WriteLine($"Warning: Skipping unsupported tar entry type '{entry.EntryType}' for '{entry.Name}'.");
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
        installTask?.MaxValue = zip.Entries.Count > 0 ? zip.Entries.Count : 1;

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
