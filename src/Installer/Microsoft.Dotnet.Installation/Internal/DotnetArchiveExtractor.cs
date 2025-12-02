// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        var muxerConfig = new MuxerHandlingConfig(muxerName, muxerTargetPath, archivePath, targetDir);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ExtractTarArchive(archivePath, targetDir, muxerConfig, installTask);
        }
        else
        {
            ExtractZipArchive(archivePath, targetDir, muxerConfig, installTask);
        }
    }

    /// <summary>
    /// Determines if the new muxer should replace the existing one by comparing file versions.
    /// </summary>
    /// <param name="newMuxerPath">Path to the new muxer file (extracted to temp location)</param>
    /// <param name="existingMuxerPath">Path to the existing muxer file</param>
    /// <param name="archivePath">Path to the archive being installed (used to determine runtime version on non-Windows)</param>
    /// <param name="installRoot">Install root directory (used to find existing runtime versions on non-Windows)</param>
    /// <returns>True if the new muxer should replace the existing one</returns>
    internal static bool ShouldUpdateMuxer(string newMuxerPath, string existingMuxerPath, string? archivePath = null, string? installRoot = null)
    {
        // If there's no existing muxer, we should install the new one
        if (!File.Exists(existingMuxerPath))
        {
            return true;
        }

        // Compare file versions
        Version? existingVersion = GetMuxerFileVersion(existingMuxerPath, installRoot);
        Version? newVersion = GetMuxerFileVersion(newMuxerPath, archivePath);

        // If we can't determine the new version, don't update (safety measure)
        if (newVersion is null)
        {
            return false;
        }

        // If we can't determine the existing version, update to the new one
        if (existingVersion is null)
        {
            return true;
        }

        // Only update if the new version is greater than the existing version
        return newVersion > existingVersion;
    }

    /// <summary>
    /// Gets the file version of a muxer executable.
    /// On Windows, uses FileVersionInfo. On other platforms, uses runtime version from archive/install root.
    /// </summary>
    /// <param name="muxerPath">Path to the muxer executable</param>
    /// <param name="contextPath">Archive path or install root path for fallback version detection</param>
    internal static Version? GetMuxerFileVersion(string muxerPath, string? contextPath = null)
    {
        if (!File.Exists(muxerPath))
        {
            return null;
        }

        try
        {
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(muxerPath);
            if (versionInfo.FileVersion is not null && Version.TryParse(versionInfo.FileVersion, out Version? version))
            {
                return version;
            }

            // Fallback to constructing version from individual parts
            if (versionInfo.FileMajorPart > 0 || versionInfo.FileMinorPart > 0)
            {
                return new Version(
                    versionInfo.FileMajorPart,
                    versionInfo.FileMinorPart,
                    versionInfo.FileBuildPart,
                    versionInfo.FilePrivatePart);
            }

            // On non-Windows, FileVersionInfo doesn't work with ELF binaries
            // Fall back to using runtime version from context path
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && contextPath != null)
            {
                return GetRuntimeVersionFromContext(contextPath);
            }

            return null;
        }
        catch
        {
            // On error, try fallback for non-Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && contextPath != null)
            {
                return GetRuntimeVersionFromContext(contextPath);
            }
            return null;
        }
    }

    /// <summary>
    /// Gets the runtime version from a context path (either an archive or install root directory).
    /// </summary>
    private static Version? GetRuntimeVersionFromContext(string contextPath)
    {
        if (string.IsNullOrEmpty(contextPath))
        {
            return null;
        }

        // Check if it's an archive file
        if (File.Exists(contextPath))
        {
            return GetRuntimeVersionFromArchive(contextPath);
        }

        // Otherwise treat it as an install root directory
        if (Directory.Exists(contextPath))
        {
            return GetLatestRuntimeVersionFromInstallRoot(contextPath);
        }

        return null;
    }

    /// <summary>
    /// Gets the runtime version from an archive by examining the runtime directories.
    /// </summary>
    private static Version? GetRuntimeVersionFromArchive(string archivePath)
    {
        try
        {
            if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var zip = ZipFile.OpenRead(archivePath);
                return GetRuntimeVersionFromZipEntries(zip.Entries);
            }
            else if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || archivePath.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
            {
                string tarPath = archivePath;
                bool needsCleanup = false;

                if (archivePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                {
                    tarPath = DecompressTarGzToTemp(archivePath);
                    needsCleanup = true;
                }

                try
                {
                    using var tarStream = File.OpenRead(tarPath);
                    using var tarReader = new TarReader(tarStream);
                    return GetRuntimeVersionFromTarEntries(tarReader);
                }
                finally
                {
                    if (needsCleanup && File.Exists(tarPath))
                    {
                        File.Delete(tarPath);
                    }
                }
            }
        }
        catch
        {
            // If we can't read the archive, return null
        }

        return null;
    }

    /// <summary>
    /// Decompresses a .tar.gz file to a temporary location.
    /// </summary>
    private static string DecompressTarGzToTemp(string gzPath)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"dotnet-{Guid.NewGuid()}.tar");
        using FileStream originalFileStream = File.OpenRead(gzPath);
        using FileStream decompressedFileStream = File.Create(tempPath);
        using GZipStream decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress);
        decompressionStream.CopyTo(decompressedFileStream);
        return tempPath;
    }

    /// <summary>
    /// Gets the runtime version from zip archive entries.
    /// </summary>
    private static Version? GetRuntimeVersionFromZipEntries(IEnumerable<ZipArchiveEntry> entries)
    {
        Version? highestVersion = null;

        foreach (var entry in entries)
        {
            // Look for shared/Microsoft.NETCore.App/{version}/ pattern
            if (entry.FullName.Contains("shared/Microsoft.NETCore.App/", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.Contains("shared\\Microsoft.NETCore.App\\", StringComparison.OrdinalIgnoreCase))
            {
                var parts = entry.FullName.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                var appIndex = Array.FindIndex(parts, p => p.Equals("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase));

                if (appIndex >= 0 && appIndex + 1 < parts.Length)
                {
                    var versionString = parts[appIndex + 1];
                    if (Version.TryParse(versionString, out Version? version))
                    {
                        if (highestVersion == null || version > highestVersion)
                        {
                            highestVersion = version;
                        }
                    }
                }
            }
        }

        return highestVersion;
    }

    /// <summary>
    /// Gets the runtime version from tar archive entries.
    /// </summary>
    private static Version? GetRuntimeVersionFromTarEntries(TarReader tarReader)
    {
        Version? highestVersion = null;
        TarEntry? entry;

        while ((entry = tarReader.GetNextEntry()) is not null)
        {
            // Look for shared/Microsoft.NETCore.App/{version}/ pattern
            if (entry.Name.Contains("shared/Microsoft.NETCore.App/", StringComparison.OrdinalIgnoreCase))
            {
                var parts = entry.Name.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                var appIndex = Array.FindIndex(parts, p => p.Equals("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase));

                if (appIndex >= 0 && appIndex + 1 < parts.Length)
                {
                    var versionString = parts[appIndex + 1];
                    if (Version.TryParse(versionString, out Version? version))
                    {
                        if (highestVersion == null || version > highestVersion)
                        {
                            highestVersion = version;
                        }
                    }
                }
            }
        }

        return highestVersion;
    }

    /// <summary>
    /// Gets the latest runtime version from the install root by checking the shared/Microsoft.NETCore.App directory.
    /// </summary>
    private static Version? GetLatestRuntimeVersionFromInstallRoot(string installRoot)
    {
        try
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
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts a tar or tar.gz archive to the target directory.
    /// </summary>
    private void ExtractTarArchive(string archivePath, string targetDir, MuxerHandlingConfig muxerConfig, IProgressTask? installTask)
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

    /// <summary>
    /// Extracts a single file entry from a tar archive.
    /// </summary>
    private void ExtractTarFileEntry(TarEntry entry, string targetDir, MuxerHandlingConfig muxerConfig, IProgressTask? installTask)
    {
        var fileName = Path.GetFileName(entry.Name);
        var destPath = Path.Combine(targetDir, entry.Name);

        if (string.Equals(fileName, muxerConfig.MuxerName, StringComparison.OrdinalIgnoreCase))
        {
            HandleMuxerFromTar(entry, muxerConfig);
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            using var outStream = File.Create(destPath);
            entry.DataStream?.CopyTo(outStream);
        }

        installTask?.Value += 1;
    }

    /// <summary>
    /// Handles the muxer from a tar entry, comparing file versions to determine if update is needed.
    /// </summary>
    private void HandleMuxerFromTar(TarEntry entry, MuxerHandlingConfig muxerConfig)
    {
        // Create a temporary file for the muxer first
        var tempMuxerPath = Path.Combine(Directory.CreateTempSubdirectory().FullName, entry.Name);
        using (var outStream = File.Create(tempMuxerPath))
        {
            entry.DataStream?.CopyTo(outStream);
        }

        try
        {
            // Check if we should update the muxer based on file version comparison
            if (ShouldUpdateMuxer(tempMuxerPath, muxerConfig.MuxerTargetPath, muxerConfig.ArchivePath, muxerConfig.InstallRoot))
            {
                // Replace the muxer using the utility that handles locking
                DotnetupUtilities.ForceReplaceFile(tempMuxerPath, muxerConfig.MuxerTargetPath);
            }
        }
        finally
        {
            if (File.Exists(tempMuxerPath))
            {
                File.Delete(tempMuxerPath);
            }
        }
    }

    /// <summary>
    /// Extracts a zip archive to the target directory.
    /// </summary>
    private void ExtractZipArchive(string archivePath, string targetDir, MuxerHandlingConfig muxerConfig, IProgressTask? installTask)
    {
        long totalFiles = CountZipEntries(archivePath);

        if (installTask is not null)
        {
            installTask.MaxValue = totalFiles > 0 ? totalFiles : 1;
        }

        using var zip = ZipFile.OpenRead(archivePath);
        foreach (var entry in zip.Entries)
        {
            ExtractZipEntry(entry, targetDir, muxerConfig, installTask);
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
            HandleMuxerFromZip(entry, muxerConfig);
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            entry.ExtractToFile(destPath, overwrite: true);
        }

        installTask?.Value += 1;
    }

    /// <summary>
    /// Handles the muxer from a zip entry, comparing file versions to determine if update is needed.
    /// </summary>
    private void HandleMuxerFromZip(ZipArchiveEntry entry, MuxerHandlingConfig muxerConfig)
    {
        var tempMuxerPath = Path.Combine(Directory.CreateTempSubdirectory().FullName, entry.Name);
        entry.ExtractToFile(tempMuxerPath, overwrite: true);

        try
        {
            // Check if we should update the muxer based on file version comparison
            if (ShouldUpdateMuxer(tempMuxerPath, muxerConfig.MuxerTargetPath, muxerConfig.ArchivePath, muxerConfig.InstallRoot))
            {
                // Replace the muxer using the utility that handles locking
                DotnetupUtilities.ForceReplaceFile(tempMuxerPath, muxerConfig.MuxerTargetPath);
            }
        }
        finally
        {
            if (File.Exists(tempMuxerPath))
            {
                File.Delete(tempMuxerPath);
            }
        }
    }

    /// <summary>
    /// Configuration class for muxer handling.
    /// </summary>
    private readonly struct MuxerHandlingConfig
    {
        public string MuxerName { get; }
        public string MuxerTargetPath { get; }
        public string ArchivePath { get; }
        public string InstallRoot { get; }

        public MuxerHandlingConfig(string muxerName, string muxerTargetPath, string archivePath, string installRoot)
        {
            MuxerName = muxerName;
            MuxerTargetPath = muxerTargetPath;
            ArchivePath = archivePath;
            InstallRoot = installRoot;
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
}
