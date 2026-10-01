// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    private readonly ITarArchiveExtractor _tarArchiveExtractor;
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
        var dotnetTarArchiveExtractor = new DotnetTarArchiveExtractor();
        _tarArchiveExtractor = OperatingSystem.IsWindows()
            ? new WindowsNativeTarArchiveExtractor(dotnetTarArchiveExtractor)
            : dotnetTarArchiveExtractor;
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
            _tarArchiveExtractor.Extract(new TarExtractionContext(
                archivePath,
                targetDir,
                installTask,
                MuxerHandler,
                TrackSubcomponent,
                shouldSkipEntry));
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

    internal static void ExtractTarArchive(string archivePath, string targetDir, IProgressTask? installTask, MuxerHandler? muxerHandler = null, Action<string>? onEntryExtracted = null, Func<string, bool>? shouldSkipEntry = null)
        => new DotnetTarArchiveExtractor().Extract(
            new TarExtractionContext(
                archivePath,
                targetDir,
                installTask,
                muxerHandler,
                onEntryExtracted,
                shouldSkipEntry));

    /// <summary>
    /// Extracts a zip archive to the target directory.
    /// </summary>
    private static void ExtractZipArchive(string archivePath, string targetDir, IProgressTask? installTask, MuxerHandler? muxerHandler = null, Action<string>? onEntryExtracted = null, Func<string, bool>? shouldSkipEntry = null)
    {
        using var zip = ZipFile.OpenRead(archivePath);
        IProgressTask? entryProgressTask = installTask?.RequiresKnownMaximum == true ? installTask : null;
        if (entryProgressTask is not null)
        {
            entryProgressTask.MaxValue = zip.Entries.Count > 0 ? zip.Entries.Count : 1;
        }

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
                    string destPath = ArchiveEntryPathResolver.ResolveDestination(entry.FullName, targetDir, muxerHandler);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    entry.ExtractToFile(destPath, overwrite: true);
                }
            }

            onEntryExtracted?.Invoke(entry.FullName);
            entryProgressTask?.Value += 1;
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
