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

internal class DotnetArchiveExtractor : IDisposable
{
    private readonly DotnetInstallRequest _request;
    private readonly ReleaseVersion _resolvedVersion;
    private readonly IProgressTarget _progressTarget;
    private string scratchDownloadDirectory;
    private string? _archivePath;

    public DotnetArchiveExtractor(DotnetInstallRequest request, ReleaseVersion resolvedVersion, ReleaseManifest releaseManifest, IProgressTarget progressTarget)
    {
        _request = request;
        _resolvedVersion = resolvedVersion;
        _progressTarget = progressTarget;
        scratchDownloadDirectory = Directory.CreateTempSubdirectory().FullName;
    }

    public void Prepare()
    {
        using var activity = InstallationActivitySource.ActivitySource.StartActivity("DotnetInstaller.Prepare");

        using var archiveDownloader = new DotnetArchiveDownloader();
        var archiveName = $"dotnet-{Guid.NewGuid()}";
        _archivePath = Path.Combine(scratchDownloadDirectory, archiveName + DotnetupUtilities.GetArchiveFileExtensionForPlatform());

        using (var progressReporter = _progressTarget.CreateProgressReporter())
        {
            var downloadTask = progressReporter.AddTask($"Downloading .NET SDK {_resolvedVersion}", 100);
            var reporter = new DownloadProgressReporter(downloadTask, $"Downloading .NET SDK {_resolvedVersion}");

            try
            {
                archiveDownloader.DownloadArchiveWithVerification(_request, _resolvedVersion, _archivePath, reporter);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to download .NET archive for version {_resolvedVersion}", ex);
            }

            downloadTask.Value = 100;
        }
    }
    public void Commit()
    {
        using var activity = InstallationActivitySource.ActivitySource.StartActivity("DotnetInstaller.Commit");

        using (var progressReporter = _progressTarget.CreateProgressReporter())
        {
            var installTask = progressReporter.AddTask($"Installing .NET SDK {_resolvedVersion}", maxValue: 100);

            // Extract archive directly to target directory with special handling for muxer
            ExtractArchiveDirectlyToTarget(_archivePath!, _request.InstallRoot.Path!, installTask);
            installTask.Value = installTask.MaxValue;
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
        string muxerTempPath = muxerTargetPath + ".tmp";

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
            if (File.Exists(muxerTempPath))
            {
                File.Delete(muxerTempPath);
            }
            File.Move(muxerTargetPath, muxerTempPath);
        }

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
            }
            else
            {
                // Latest runtime version increased (or we couldn't determine versions) - keep new muxer
                if (File.Exists(muxerTempPath))
                {
                    File.Delete(muxerTempPath);
                }
            }
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
    }

    public void Dispose()
    {
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
