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
using Microsoft.DotNet.NativeWrapper;

namespace Microsoft.Dotnet.Installation.Internal;

internal class DotnetArchiveExtractor : IDisposable
{
    private readonly DotnetInstallRequest _request;
    private readonly ReleaseVersion _resolvedVersion;
    private readonly ReleaseManifest _releaseManifest;
    private readonly IProgressTarget _progressTarget;
    private string scratchDownloadDirectory;
    private string? _archivePath;

    public DotnetArchiveExtractor(DotnetInstallRequest request, ReleaseVersion resolvedVersion, ReleaseManifest releaseManifest, IProgressTarget progressTarget)
    {
        _request = request;
        _resolvedVersion = resolvedVersion;
        _releaseManifest = releaseManifest ?? new();
        _progressTarget = progressTarget;
        scratchDownloadDirectory = Directory.CreateTempSubdirectory().FullName;
    }

    public void Prepare()
    {
        using var archiveDownloader = new DotnetArchiveDownloader(_releaseManifest);
        var archiveName = $"dotnet-{Guid.NewGuid()}";
        _archivePath = Path.Combine(scratchDownloadDirectory, archiveName + DnupUtilities.GetArchiveFileExtensionForPlatform());

        using (var progressReporter = _progressTarget.CreateProgressReporter())
        {
            var downloadTask = progressReporter.AddTask($"Downloading .NET SDK {_resolvedVersion}", 100);
            var reporter = new DownloadProgressReporter(downloadTask, $"Downloading .NET SDK {_resolvedVersion}");
            var downloadSuccess = archiveDownloader.DownloadArchiveWithVerification(_request, _resolvedVersion, _archivePath, reporter);
            if (!downloadSuccess)
            {
                throw new InvalidOperationException($"Failed to download .NET archive for version {_resolvedVersion}");
            }

            Console.WriteLine($"Download of .NET SDK {_resolvedVersion} complete.");
            downloadTask.Value = 100;
        }
    }

    public void Commit()
    {
        Commit(GetExistingSdkVersions(_request.InstallRoot));
    }

    public void Commit(IEnumerable<ReleaseVersion> existingSdkVersions)
    {
        if (_archivePath == null || !File.Exists(_archivePath))
        {
            throw new InvalidOperationException("Archive not found. Make sure Prepare() was called successfully.");
        }

        using (var progressReporter = _progressTarget.CreateProgressReporter())
        {
            var installTask = progressReporter.AddTask($"Installing .NET SDK {_resolvedVersion}", maxValue: 100);

            // Extract archive directly to target directory with special handling for muxer
            var extractResult = ExtractArchiveDirectlyToTarget(_archivePath, _request.InstallRoot.Path!, existingSdkVersions, installTask);
            if (extractResult is not null)
            {
                throw new InvalidOperationException($"Failed to install SDK: {extractResult}");
            }

            installTask.Value = installTask.MaxValue;
        }
    }

    /**
     * Extracts the archive directly to the target directory with special handling for muxer.
     * Combines extraction and installation into a single operation.
     */
    private string? ExtractArchiveDirectlyToTarget(string archivePath, string targetDir, IEnumerable<ReleaseVersion> existingSdkVersions, IProgressTask? installTask)
    {
        Directory.CreateDirectory(targetDir);

        var muxerConfig = ConfigureMuxerHandling(existingSdkVersions);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ExtractTarArchive(archivePath, targetDir, muxerConfig, installTask);
        }
        else
        {
            return ExtractZipArchive(archivePath, targetDir, muxerConfig, installTask);
        }
    }

    /**
     * Configure muxer handling by determining if it needs to be updated.
     */
    private MuxerHandlingConfig ConfigureMuxerHandling(IEnumerable<ReleaseVersion> existingSdkVersions)
    {
        // TODO: This is very wrong - its comparing a runtime version and sdk version, plus it needs to respect the muxer version
        ReleaseVersion? existingMuxerVersion = existingSdkVersions.Any() ? existingSdkVersions.Max() : (ReleaseVersion?)null;
        ReleaseVersion newRuntimeVersion = _resolvedVersion;
        bool shouldUpdateMuxer = existingMuxerVersion is null || newRuntimeVersion.CompareTo(existingMuxerVersion) > 0;

        string muxerName = DnupUtilities.GetDotnetExeName();
        string muxerTargetPath = Path.Combine(_request.InstallRoot.Path!, muxerName);

        return new MuxerHandlingConfig(
            muxerName,
            muxerTargetPath,
            shouldUpdateMuxer);
    }

    /**
     * Extracts a tar or tar.gz archive to the target directory.
     */
    private string? ExtractTarArchive(string archivePath, string targetDir, MuxerHandlingConfig muxerConfig, IProgressTask? installTask)
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
            ExtractTarContents(decompressedPath, targetDir, muxerConfig, installTask);

            return null;
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

    /**
     * Decompresses a .tar.gz file if needed, returning the path to the tar file.
     */
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

    /**
     * Counts the number of entries in a tar file for progress reporting.
     */
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

    /**
     * Extracts the contents of a tar file to the target directory.
     */
    private void ExtractTarContents(string tarPath, string targetDir, MuxerHandlingConfig muxerConfig, IProgressTask? installTask)
    {
        using var tarStream = File.OpenRead(tarPath);
        var tarReader = new TarReader(tarStream);
        TarEntry? entry;

        while ((entry = tarReader.GetNextEntry()) is not null)
        {
            if (entry.EntryType == TarEntryType.RegularFile)
            {
                ExtractTarFileEntry(entry, targetDir, muxerConfig, installTask);
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

    /**
     * Extracts a single file entry from a tar archive.
     */
    private void ExtractTarFileEntry(TarEntry entry, string targetDir, MuxerHandlingConfig muxerConfig, IProgressTask? installTask)
    {
        var fileName = Path.GetFileName(entry.Name);
        var destPath = Path.Combine(targetDir, entry.Name);

        if (string.Equals(fileName, muxerConfig.MuxerName, StringComparison.OrdinalIgnoreCase))
        {
            if (muxerConfig.ShouldUpdateMuxer)
            {
                HandleMuxerUpdateFromTar(entry, muxerConfig.MuxerTargetPath);
            }
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            using var outStream = File.Create(destPath);
            entry.DataStream?.CopyTo(outStream);
        }

        installTask?.Value += 1;
    }

    /**
     * Handles updating the muxer from a tar entry, using a temporary file to avoid locking issues.
     */
    private void HandleMuxerUpdateFromTar(TarEntry entry, string muxerTargetPath)
    {
        // Create a temporary file for the muxer first to avoid locking issues
        var tempMuxerPath = Directory.CreateTempSubdirectory().FullName;
        using (var outStream = File.Create(tempMuxerPath))
        {
            entry.DataStream?.CopyTo(outStream);
        }

        try
        {
            // Replace the muxer using the utility that handles locking
            DnupUtilities.ForceReplaceFile(tempMuxerPath, muxerTargetPath);
        }
        finally
        {
            if (File.Exists(tempMuxerPath))
            {
                File.Delete(tempMuxerPath);
            }
        }
    }

    /**
     * Extracts a zip archive to the target directory.
     */
    private string? ExtractZipArchive(string archivePath, string targetDir, MuxerHandlingConfig muxerConfig, IProgressTask? installTask)
    {
        long totalFiles = CountZipEntries(archivePath);

        installTask?.MaxValue = totalFiles > 0 ? totalFiles : 1;

        using var zip = ZipFile.OpenRead(archivePath);
        foreach (var entry in zip.Entries)
        {
            ExtractZipEntry(entry, targetDir, muxerConfig, installTask);
        }

        return null;
    }

    /**
     * Counts the number of entries in a zip file for progress reporting.
     */
    private long CountZipEntries(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        return zip.Entries.Count;
    }

    /**
     * Extracts a single entry from a zip archive.
     */
    private void ExtractZipEntry(ZipArchiveEntry entry, string targetDir, MuxerHandlingConfig muxerConfig, IProgressTask? installTask)
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

        // Special handling for dotnet executable (muxer)
        if (string.Equals(fileName, muxerConfig.MuxerName, StringComparison.OrdinalIgnoreCase))
        {
            if (muxerConfig.ShouldUpdateMuxer)
            {
                HandleMuxerUpdateFromZip(entry, muxerConfig.MuxerTargetPath);
            }
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            entry.ExtractToFile(destPath, overwrite: true);
        }

        installTask?.Value += 1;
    }

    /**
     * Handles updating the muxer from a zip entry, using a temporary file to avoid locking issues.
     */
    private void HandleMuxerUpdateFromZip(ZipArchiveEntry entry, string muxerTargetPath)
    {
        var tempMuxerPath = Directory.CreateTempSubdirectory().FullName;
        entry.ExtractToFile(tempMuxerPath, overwrite: true);

        try
        {
            // Replace the muxer using the utility that handles locking
            DnupUtilities.ForceReplaceFile(tempMuxerPath, muxerTargetPath);
        }
        finally
        {
            if (File.Exists(tempMuxerPath))
            {
                File.Delete(tempMuxerPath);
            }
        }
    }

    /**
     * Configuration class for muxer handling.
     */
    private readonly struct MuxerHandlingConfig
    {
        public string MuxerName { get; }
        public string MuxerTargetPath { get; }
        public bool ShouldUpdateMuxer { get; }

        public MuxerHandlingConfig(string muxerName, string muxerTargetPath, bool shouldUpdateMuxer)
        {
            MuxerName = muxerName;
            MuxerTargetPath = muxerTargetPath;
            ShouldUpdateMuxer = shouldUpdateMuxer;
        }
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

    private static IEnumerable<ReleaseVersion> GetExistingSdkVersions(DotnetInstallRoot installRoot)
    {
        var environmentInfo = HostFxrWrapper.getInfo(installRoot.Path!);
        return environmentInfo.SdkInfo.Select(sdk => sdk.Version);
    }
}
