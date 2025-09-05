// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class ArchiveDotnetInstaller : IDotnetInstaller, IDisposable
{
    private readonly DotnetInstallRequest _request;
    private readonly DotnetInstall _install;
    private string scratchDownloadDirectory;
    private string? _archivePath;

    public ArchiveDotnetInstaller(DotnetInstallRequest request, DotnetInstall version)
    {
        _request = request;
        _install = version;
        scratchDownloadDirectory = Directory.CreateTempSubdirectory().FullName;
    }

    public void Prepare()
    {
        using var releaseManifest = new ReleaseManifest();
        var archiveName = $"dotnet-{_install.Id}";
        _archivePath = Path.Combine(scratchDownloadDirectory, archiveName + DnupUtilities.GetFileExtensionForPlatform());

        Spectre.Console.AnsiConsole.Progress()
            .Start(ctx =>
            {
                var downloadTask = ctx.AddTask($"Downloading .NET SDK {_install.FullySpecifiedVersion.Value}", autoStart: true);
                var reporter = new SpectreDownloadProgressReporter(downloadTask, $"Downloading .NET SDK {_install.FullySpecifiedVersion.Value}");
                var downloadSuccess = releaseManifest.DownloadArchiveWithVerification(_install, _archivePath, reporter);
                if (!downloadSuccess)
                {
                    throw new InvalidOperationException($"Failed to download .NET archive for version {_install.FullySpecifiedVersion.Value}");
                }

                downloadTask.Value = 100;
            });
    }

    /**
    Returns a string if the archive is valid within SDL specification, false otherwise.
    */
    private void VerifyArchive(string archivePath)
    {
        if (!File.Exists(archivePath)) // Enhancement: replace this with actual verification logic once its implemented.
        {
            throw new InvalidOperationException("Archive verification failed.");
        }
    }



    internal static string ConstructArchiveName(string? versionString, string rid, string suffix)
    {
        return versionString is null
            ? $"dotnet-sdk-{rid}{suffix}"
            : $"dotnet-sdk-{versionString}-{rid}{suffix}";
    }



    public void Commit()
    {
        Commit(GetExistingSdkVersions(_request.TargetDirectory));
    }

    public void Commit(IEnumerable<DotnetVersion> existingSdkVersions)
    {
        if (_archivePath == null || !File.Exists(_archivePath))
        {
            throw new InvalidOperationException("Archive not found. Make sure Prepare() was called successfully.");
        }

        Spectre.Console.AnsiConsole.Progress()
            .Start(ctx =>
            {
                var installTask = ctx.AddTask($"Installing .NET SDK {_install.FullySpecifiedVersion.Value}", autoStart: true);

                // Extract archive directly to target directory with special handling for muxer
                var extractResult = ExtractArchiveDirectlyToTarget(_archivePath, _request.TargetDirectory, existingSdkVersions, installTask);
                if (extractResult != null)
                {
                    throw new InvalidOperationException($"Failed to install SDK: {extractResult}");
                }

                installTask.Value = installTask.MaxValue;
            });
    }

    /**
     * Extracts the archive directly to the target directory with special handling for muxer.
     * Combines extraction and installation into a single operation.
     */
    private string? ExtractArchiveDirectlyToTarget(string archivePath, string targetDir, IEnumerable<DotnetVersion> existingSdkVersions, Spectre.Console.ProgressTask? installTask)
    {
        try
        {
            // Ensure target directory exists
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
        catch (Exception e)
        {
            return e.Message;
        }
    }

    /**
     * Configure muxer handling by determining if it needs to be updated.
     */
    private MuxerHandlingConfig ConfigureMuxerHandling(IEnumerable<DotnetVersion> existingSdkVersions)
    {
        DotnetVersion? existingMuxerVersion = existingSdkVersions.Any() ? existingSdkVersions.Max() : (DotnetVersion?)null;
        DotnetVersion newRuntimeVersion = _install.FullySpecifiedVersion;
        bool shouldUpdateMuxer = existingMuxerVersion is null || newRuntimeVersion.CompareTo(existingMuxerVersion) > 0;

        string muxerName = DnupUtilities.GetDotnetExeName();
        string muxerTargetPath = Path.Combine(_request.TargetDirectory, muxerName);

        return new MuxerHandlingConfig(
            muxerName,
            muxerTargetPath,
            shouldUpdateMuxer);
    }

    /**
     * Extracts a tar or tar.gz archive to the target directory.
     */
    private string? ExtractTarArchive(string archivePath, string targetDir, MuxerHandlingConfig muxerConfig, Spectre.Console.ProgressTask? installTask)
    {
        string decompressedPath = DecompressTarGzIfNeeded(archivePath, out bool needsDecompression);

        try
        {
            // Count files in tar for progress reporting
            long totalFiles = CountTarEntries(decompressedPath);

            // Set progress maximum
            if (installTask != null)
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

        string decompressedPath = Path.Combine(
            Path.GetDirectoryName(archivePath) ?? Directory.CreateTempSubdirectory().FullName,
            "decompressed.tar");

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
        while (tarReader.GetNextEntry() != null)
        {
            totalFiles++;
        }
        return totalFiles;
    }

    /**
     * Extracts the contents of a tar file to the target directory.
     */
    private void ExtractTarContents(string tarPath, string targetDir, MuxerHandlingConfig muxerConfig, Spectre.Console.ProgressTask? installTask)
    {
        using var tarStream = File.OpenRead(tarPath);
        var tarReader = new TarReader(tarStream);
        TarEntry? entry;

        while ((entry = tarReader.GetNextEntry()) != null)
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
                installTask?.Increment(1);
            }
            else
            {
                // Skip other entry types
                installTask?.Increment(1);
            }
        }
    }

    /**
     * Extracts a single file entry from a tar archive.
     */
    private void ExtractTarFileEntry(TarEntry entry, string targetDir, MuxerHandlingConfig muxerConfig, Spectre.Console.ProgressTask? installTask)
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

        installTask?.Increment(1);
    }

    /**
     * Handles updating the muxer from a tar entry, using a temporary file to avoid locking issues.
     */
    private void HandleMuxerUpdateFromTar(TarEntry entry, string muxerTargetPath)
    {
        // Create a temporary file for the muxer first to avoid locking issues
        var tempMuxerPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
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
    private string? ExtractZipArchive(string archivePath, string targetDir, MuxerHandlingConfig muxerConfig, Spectre.Console.ProgressTask? installTask)
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
    private void ExtractZipEntry(ZipArchiveEntry entry, string targetDir, MuxerHandlingConfig muxerConfig, Spectre.Console.ProgressTask? installTask)
    {
        var fileName = Path.GetFileName(entry.FullName);
        var destPath = Path.Combine(targetDir, entry.FullName);

        // Skip directories (we'll create them for files as needed)
        if (string.IsNullOrEmpty(fileName))
        {
            Directory.CreateDirectory(destPath);
            installTask?.Increment(1);
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

        installTask?.Increment(1);
    }

    /**
     * Handles updating the muxer from a zip entry, using a temporary file to avoid locking issues.
     */
    private void HandleMuxerUpdateFromZip(ZipArchiveEntry entry, string muxerTargetPath)
    {
        var tempMuxerPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
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

    // This should be cached and more sophisticated based on vscode logic in the future
    private IEnumerable<DotnetVersion> GetExistingSdkVersions(string targetDirectory)
    {
        var dotnetExe = Path.Combine(targetDirectory, DnupUtilities.GetDotnetExeName());
        if (!File.Exists(dotnetExe))
            return Enumerable.Empty<DotnetVersion>();

        try
        {
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = dotnetExe;
            process.StartInfo.Arguments = "--list-sdks";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var versions = new List<DotnetVersion>();
            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(' ');
                if (parts.Length > 0)
                {
                    var versionStr = parts[0];
                    if (DotnetVersion.TryParse(versionStr, out var version))
                    {
                        versions.Add(version);
                    }
                }
            }
            return versions;
        }
        catch
        {
            return [];
        }
    }
}
