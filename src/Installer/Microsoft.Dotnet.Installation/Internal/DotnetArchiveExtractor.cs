// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
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
    private int _extractedFileCount;
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
        using var activity = InstallationActivitySource.ActivitySource.StartActivity("download");
        activity?.SetTag("download.version", _resolvedVersion.ToString());

        var archiveName = $"dotnet-{Guid.NewGuid()}";
        _archivePath = Path.Combine(scratchDownloadDirectory, archiveName + DotnetupUtilities.GetArchiveFileExtensionForPlatform());

        string componentDescription = _request.Component.GetDisplayName();
        var downloadTask = ProgressReporter.AddTask($"Downloading {componentDescription} {_resolvedVersion}", 100);
        var reporter = new DownloadProgressReporter(downloadTask, $"Downloading {componentDescription} {_resolvedVersion}");

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

        string componentDescription = _request.Component.GetDisplayName();
        var installTask = ProgressReporter.AddTask($"Installing {componentDescription} {_resolvedVersion}", maxValue: 100);

        try
        {
            // Extract archive directly to target directory with special handling for muxer
            ExtractArchiveDirectlyToTarget(_archivePath!, _request.InstallRoot.Path!, installTask);
            installTask.Value = installTask.MaxValue;
        }
        catch (DotnetInstallException)
        {
            throw;
        }
        catch (InvalidDataException ex)
        {
            // Archive is corrupted (invalid zip/tar format)
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ArchiveCorrupted,
                $"Archive is corrupted or truncated for version {_resolvedVersion}: {ex.Message}",
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

        string muxerName = DotnetupUtilities.GetDotnetExeName();
        string muxerTargetPath = Path.Combine(targetDir, muxerName);
        string muxerTempPath = $"{muxerTargetPath}.{Guid.NewGuid().ToString()}.tmp";

        // Step 1: Read the version of the existing muxer (if any) by looking at the latest runtime
        Version? existingMuxerVersion = null;
        bool hadExistingMuxer = File.Exists(muxerTargetPath);
        if (hadExistingMuxer)
        {
            existingMuxerVersion = GetLatestRuntimeVersionFromInstallRoot(targetDir);
        }

        // Step 2: If there is an existing muxer, rename it to .tmp
        if (hadExistingMuxer)
        {
            File.Move(muxerTargetPath, muxerTempPath);
        }

        try
        {
            // Step 3: Extract the archive (all files directly since muxer has been renamed)
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ExtractTarArchive(archivePath, targetDir, installTask);
            }
            else
            {
                ExtractZipArchive(archivePath, targetDir, installTask);
            }

            // Step 4: If there was a previous muxer, compare versions and restore if needed
            if (hadExistingMuxer && File.Exists(muxerTempPath))
            {
                Version? newMuxerVersion = GetLatestRuntimeVersionFromInstallRoot(targetDir);

                // If the latest runtime version after extraction is the same as before,
                // then a newer runtime was NOT installed, so the new muxer is actually older.
                // In that case, restore the old muxer.
                if (newMuxerVersion != null && existingMuxerVersion != null && newMuxerVersion == existingMuxerVersion)
                {
                    if (File.Exists(muxerTargetPath))
                    {
                        File.Delete(muxerTargetPath);
                    }
                    File.Move(muxerTempPath, muxerTargetPath);
                    Activity.Current?.SetTag("muxer.action", "kept_existing");
                    installTask?.SetTag("muxer.action", "kept_existing");
                }
                else
                {
                    // Latest runtime version increased (or we couldn't determine versions) - keep new muxer
                    if (File.Exists(muxerTempPath))
                    {
                        File.Delete(muxerTempPath);
                    }
                    Activity.Current?.SetTag("muxer.action", "updated");
                    Activity.Current?.SetTag("muxer.previous_version", existingMuxerVersion?.ToString() ?? "unknown");
                    Activity.Current?.SetTag("muxer.new_version", newMuxerVersion?.ToString() ?? "unknown");
                    installTask?.SetTag("muxer.action", "updated");
                    installTask?.SetTag("muxer.previous_version", existingMuxerVersion?.ToString() ?? "unknown");
                    installTask?.SetTag("muxer.new_version", newMuxerVersion?.ToString() ?? "unknown");
                }
            }
            else if (!hadExistingMuxer)
            {
                Activity.Current?.SetTag("muxer.action", "new_install");
                installTask?.SetTag("muxer.action", "new_install");
            }
        }
        catch
        {
            // If an exception occurs during extraction or version comparison, restore the original muxer if it exists
            if (hadExistingMuxer && File.Exists(muxerTempPath) && !File.Exists(muxerTargetPath))
            {
                try
                {
                    File.Move(muxerTempPath, muxerTargetPath);
                }
                catch
                {
                    // Ignore errors during cleanup - the original exception is more important
                }
            }
            throw;
        }
    }

    /// <summary>
    /// Gets the latest runtime version from the install root by checking the shared/Microsoft.NETCore.App directory.
    /// </summary>
    private static Version? GetLatestRuntimeVersionFromInstallRoot(string installRoot)
    {
        var runtimePath = Path.Combine(installRoot, "shared", "Microsoft.NETCore.App");
        if (!Directory.Exists(runtimePath))
        {
            return null;
        }

        Version? highestVersion = null;
        foreach (var dir in Directory.GetDirectories(runtimePath))
        {
            var versionString = Path.GetFileName(dir);
            if (Version.TryParse(versionString, out Version? version))
            {
                if (highestVersion == null || version > highestVersion)
                {
                    highestVersion = version;
                }
            }
        }

        return highestVersion;
    }

    /// <summary>
    /// Extracts a tar or tar.gz archive to the target directory.
    /// </summary>
    private void ExtractTarArchive(string archivePath, string targetDir, IProgressTask? installTask)
    {
        string decompressedPath = DecompressTarGzIfNeeded(archivePath, out bool needsDecompression);

        try
        {
            // Count files in tar for progress reporting
            long totalFiles = CountTarEntries(decompressedPath);

            // Set progress maximum
            if (installTask is not null)
            {
                installTask.MaxValue = totalFiles > 0 ? totalFiles : 1;
            }

            // Extract files directly to target
            ExtractTarContents(decompressedPath, targetDir, installTask);
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
    /// </summary>
    private void ExtractTarContents(string tarPath, string targetDir, IProgressTask? installTask)
    {
        using var tarStream = File.OpenRead(tarPath);
        var tarReader = new TarReader(tarStream);
        TarEntry? entry;

        while ((entry = tarReader.GetNextEntry()) is not null)
        {
            if (entry.EntryType == TarEntryType.RegularFile)
            {
                ExtractTarFileEntry(entry, targetDir, installTask);
            }
            else if (entry.EntryType == TarEntryType.Directory)
            {
                // Create directory if it doesn't exist
                var dirPath = Path.Combine(targetDir, entry.Name);
                Directory.CreateDirectory(dirPath);
                installTask?.Value += 1;
            }
            else
            {
                // Skip other entry types
                installTask?.Value += 1;
            }
        }
    }

    /// <summary>
    /// Extracts a single file entry from a tar archive.
    /// </summary>
    private void ExtractTarFileEntry(TarEntry entry, string targetDir, IProgressTask? installTask)
    {
        var destPath = Path.Combine(targetDir, entry.Name);
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        using var outStream = File.Create(destPath);
        entry.DataStream?.CopyTo(outStream);
        installTask?.Value += 1;
        _extractedFileCount++;
    }

    /// <summary>
    /// Extracts a zip archive to the target directory.
    /// </summary>
    private void ExtractZipArchive(string archivePath, string targetDir, IProgressTask? installTask)
    {
        long totalFiles = CountZipEntries(archivePath);

        if (installTask is not null)
        {
            installTask.MaxValue = totalFiles > 0 ? totalFiles : 1;
        }

        using var zip = ZipFile.OpenRead(archivePath);
        foreach (var entry in zip.Entries)
        {
            ExtractZipEntry(entry, targetDir, installTask);
        }
    }

    /// <summary>
    /// Counts the number of entries in a zip file for progress reporting.
    /// </summary>
    private long CountZipEntries(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        return zip.Entries.Count;
    }

    /// <summary>
    /// Extracts a single entry from a zip archive.
    /// </summary>
    private void ExtractZipEntry(ZipArchiveEntry entry, string targetDir, IProgressTask? installTask)
    {
        var fileName = Path.GetFileName(entry.FullName);
        var destPath = Path.Combine(targetDir, entry.FullName);

        // Skip directories (we'll create them for files as needed)
        if (string.IsNullOrEmpty(fileName))
        {
            Directory.CreateDirectory(destPath);
            installTask?.Value += 1;
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        entry.ExtractToFile(destPath, overwrite: true);
        installTask?.Value += 1;
        _extractedFileCount++;
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
