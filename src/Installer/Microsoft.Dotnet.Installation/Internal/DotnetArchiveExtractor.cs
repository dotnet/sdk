// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

using Microsoft.Dotnet.Installation;

internal class DotnetArchiveExtractor : IDisposable
{
    private readonly DotnetInstallRequest _request;
    private readonly ReleaseVersion _resolvedVersion;
    private readonly IProgressTarget _progressTarget;
    private readonly IArchiveDownloader _archiveDownloader;
    private readonly bool _shouldDisposeDownloader;
    private string scratchDownloadDirectory;
    private string? _archivePath;
    private IProgressReporter? _progressReporter;

    public DotnetArchiveExtractor(
        DotnetInstallRequest request,
        ReleaseVersion resolvedVersion,
        ReleaseManifest releaseManifest,
        IProgressTarget progressTarget,
        IArchiveDownloader? archiveDownloader = null)
    {
        _request = request;
        _resolvedVersion = resolvedVersion;
        _progressTarget = progressTarget;
        scratchDownloadDirectory = Directory.CreateTempSubdirectory().FullName;

        if (archiveDownloader != null)
        {
            _archiveDownloader = archiveDownloader;
            _shouldDisposeDownloader = false;
        }
        else
        {
            _archiveDownloader = new DotnetArchiveDownloader(releaseManifest);
            _shouldDisposeDownloader = true;
        }
    }

    /// <summary>
    /// Gets the scratch download directory path. Exposed for testing.
    /// </summary>
    internal string ScratchDownloadDirectory => scratchDownloadDirectory;

    /// <summary>
    /// Gets or creates the shared progress reporter for both Prepare and Commit phases.
    /// This avoids multiple newlines from Spectre.Console Progress between phases.
    /// </summary>
    private IProgressReporter ProgressReporter => _progressReporter ??= _progressTarget.CreateProgressReporter();

    public void Prepare()
    {
        using var activity = InstallationActivitySource.ActivitySource.StartActivity("DotnetInstaller.Prepare");

        var archiveName = $"dotnet-{Guid.NewGuid()}";
        _archivePath = Path.Combine(scratchDownloadDirectory, archiveName + DotnetupUtilities.GetArchiveFileExtensionForPlatform());

        string componentDescription = _request.Component.GetDisplayName();
        var downloadTask = ProgressReporter.AddTask($"Downloading {componentDescription} {_resolvedVersion}", 100);
        var reporter = new DownloadProgressReporter(downloadTask, $"Downloading {componentDescription} {_resolvedVersion}");

        try
        {
            _archiveDownloader.DownloadArchiveWithVerification(_request, _resolvedVersion, _archivePath, reporter);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to download .NET archive for version {_resolvedVersion}", ex);
        }

        downloadTask.Value = 100;
    }
    public void Commit()
    {
        using var activity = InstallationActivitySource.ActivitySource.StartActivity("DotnetInstaller.Commit");

        string componentDescription = _request.Component.GetDisplayName();
        var installTask = ProgressReporter.AddTask($"Installing {componentDescription} {_resolvedVersion}", maxValue: 100);

        // Extract archive directly to target directory with special handling for muxer
        ExtractArchiveDirectlyToTarget(_archivePath!, _request.InstallRoot.Path!, installTask);
        installTask.Value = installTask.MaxValue;
    }

    /// <summary>
    /// Extracts the archive directly to the target directory with special handling for muxer.
    /// Combines extraction and installation into a single operation.
    /// </summary>
    private void ExtractArchiveDirectlyToTarget(string archivePath, string targetDir, IProgressTask? installTask)
    {
        Directory.CreateDirectory(targetDir);

        // Set up muxer handling - muxer will be extracted to temp path during main extraction
        var muxerHandler = new MuxerHandler(targetDir, _request.Options.RequireMuxerUpdate);
        muxerHandler.RecordPreExtractionState();

        // Extract everything, redirecting muxer to temp path
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ExtractTarArchive(archivePath, targetDir, installTask, muxerHandler);
        }
        else
        {
            ExtractZipArchive(archivePath, targetDir, installTask, muxerHandler);
        }

        // After extraction, decide whether to keep or discard the temp muxer
        muxerHandler.FinalizeAfterExtraction();
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

        if (muxerHandler != null && normalizedName == muxerHandler.MuxerEntryName)
        {
            return muxerHandler.GetMuxerExtractionPath();
        }

        return Path.Combine(targetDir, entryName);
    }

    /// <summary>
    /// Initializes progress tracking for extraction by setting the max value.
    /// </summary>
    private static void InitializeExtractionProgress(IProgressTask? installTask, long totalEntries)
    {
        if (installTask is not null)
        {
            installTask.MaxValue = totalEntries > 0 ? totalEntries : 1;
        }
    }

    /// <summary>
    /// Extracts a tar or tar.gz archive to the target directory.
    /// </summary>
    private void ExtractTarArchive(string archivePath, string targetDir, IProgressTask? installTask, MuxerHandler? muxerHandler = null)
    {
        string decompressedPath = DecompressTarGzIfNeeded(archivePath, out bool needsDecompression);

        try
        {
            InitializeExtractionProgress(installTask, CountTarEntries(decompressedPath));
            ExtractTarContents(decompressedPath, targetDir, installTask, muxerHandler);
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
    private string DecompressTarGzIfNeeded(string archivePath, out bool needsDecompression)
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
    private long CountTarEntries(string tarPath)
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
    internal static void ExtractTarContents(string tarPath, string targetDir, IProgressTask? installTask, MuxerHandler? muxerHandler = null)
    {
        using var tarStream = File.OpenRead(tarPath);
        var tarReader = new TarReader(tarStream);
        TarEntry? entry;

        while ((entry = tarReader.GetNextEntry()) is not null)
        {
            if (entry.EntryType == TarEntryType.RegularFile)
            {
                string destPath = ResolveEntryDestPath(entry.Name, targetDir, muxerHandler);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                // ExtractToFile handles Unix permissions automatically via entry.Mode
                entry.ExtractToFile(destPath, overwrite: true);
            }
            else if (entry.EntryType == TarEntryType.Directory)
            {
                string dirPath = Path.Combine(targetDir, entry.Name);
                Directory.CreateDirectory(dirPath);

                if (entry.Mode != default && !OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(dirPath, entry.Mode);
                }
            }

            installTask?.Value += 1;
        }
    }

    /// <summary>
    /// Extracts a zip archive to the target directory.
    /// </summary>
    private void ExtractZipArchive(string archivePath, string targetDir, IProgressTask? installTask, MuxerHandler? muxerHandler = null)
    {
        using var zip = ZipFile.OpenRead(archivePath);
        InitializeExtractionProgress(installTask, zip.Entries.Count);

        foreach (var entry in zip.Entries)
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

            installTask?.Value += 1;
        }
    }

    public void Dispose()
    {
        try
        {
            // Dispose the progress reporter to finalize progress display
            _progressReporter?.Dispose();
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
            if (Directory.Exists(scratchDownloadDirectory))
            {
                Directory.Delete(scratchDownloadDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }
}
