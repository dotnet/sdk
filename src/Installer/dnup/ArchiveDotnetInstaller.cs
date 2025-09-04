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
    private string scratchExtractionDirectory;

    public ArchiveDotnetInstaller(DotnetInstallRequest request, DotnetInstall version)
    {
        _request = request;
        _install = version;
        scratchDownloadDirectory = Directory.CreateTempSubdirectory().FullName;
        scratchExtractionDirectory = Directory.CreateTempSubdirectory().FullName;
    }

    public void Prepare()
    {
        using var releaseManifest = new ReleaseManifest();
        var archiveName = $"dotnet-{_install.Id}";
        var archivePath = Path.Combine(scratchDownloadDirectory, archiveName + DnupUtilities.GetFileExtensionForPlatform());

        Spectre.Console.AnsiConsole.Progress()
            .Start(ctx =>
            {
                var downloadTask = ctx.AddTask($"Downloading .NET SDK {_install.FullySpecifiedVersion.Value}", autoStart: true);
                var reporter = new SpectreDownloadProgressReporter(downloadTask, $"Downloading .NET SDK {_install.FullySpecifiedVersion.Value}");
                var downloadSuccess = releaseManifest.DownloadArchiveWithVerification(_install, archivePath, reporter);
                if (!downloadSuccess)
                {
                    throw new InvalidOperationException($"Failed to download .NET archive for version {_install.FullySpecifiedVersion.Value}");
                }

                downloadTask.Value = 100;

                var extractTask = ctx.AddTask($"Extracting .NET SDK {_install.FullySpecifiedVersion.Value}", autoStart: true);
                var extractResult = ExtractArchive(archivePath, scratchExtractionDirectory, extractTask);
                if (extractResult != null)
                {
                    throw new InvalidOperationException($"Failed to extract archive: {extractResult}");
                }
                extractTask.Value = extractTask.MaxValue;
            });
    }

    /**
    Returns a string if the archive is valid within SDL specification, false otherwise.
    */
    private void VerifyArchive(string archivePath)
    {
        if (!File.Exists(archivePath)) // replace this with actual verification logic once its implemented.
        {
            throw new InvalidOperationException("Archive verification failed.");
        }
    }

    /**
    Extracts the specified archive to the given extraction directory.
    The archive will be decompressed if necessary.
    Expects either a .tar.gz, .tar, or .zip archive.
    */
    private string? ExtractArchive(string archivePath, string extractionDirectory, Spectre.Console.ProgressTask extractTask)
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var needsDecompression = archivePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
                string decompressedPath = archivePath;
                if (needsDecompression)
                {
                    decompressedPath = Path.Combine(Path.GetDirectoryName(archivePath) ?? Directory.CreateTempSubdirectory().FullName, "decompressed.tar");
                    using FileStream originalFileStream = File.OpenRead(archivePath);
                    using FileStream decompressedFileStream = File.Create(decompressedPath);
                    using GZipStream decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress);
                    decompressionStream.CopyTo(decompressedFileStream);
                }
                // Count files in tar
                long totalFiles = 0;
                using (var tarStream = File.OpenRead(decompressedPath))
                {
                    var tarReader = new System.Formats.Tar.TarReader(tarStream);
                    while (tarReader.GetNextEntry() != null)
                    {
                        totalFiles++;
                    }
                }
                if (extractTask != null)
                {
                    extractTask.MaxValue = totalFiles > 0 ? totalFiles : 1;
                    using (var tarStream = File.OpenRead(decompressedPath))
                    {
                        var tarReader = new System.Formats.Tar.TarReader(tarStream);
                        System.Formats.Tar.TarEntry? entry;
                        while ((entry = tarReader.GetNextEntry()) != null)
                        {
                            if (entry.EntryType == System.Formats.Tar.TarEntryType.RegularFile)
                            {
                                var outPath = Path.Combine(extractionDirectory, entry.Name);
                                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                                using var outStream = File.Create(outPath);
                                entry.DataStream?.CopyTo(outStream);
                            }
                            extractTask.Increment(1);
                        }
                    }
                }
                // Clean up temporary decompressed file
                if (needsDecompression && File.Exists(decompressedPath))
                {
                    File.Delete(decompressedPath);
                }
            }
            else
            {
                long totalFiles = 0;
                using (var zip = ZipFile.OpenRead(archivePath))
                {
                    totalFiles = zip.Entries.Count;
                }
                if (extractTask != null)
                {
                    extractTask.MaxValue = totalFiles > 0 ? totalFiles : 1;
                    using (var zip = ZipFile.OpenRead(archivePath))
                    {
                        foreach (var entry in zip.Entries)
                        {
                            var outPath = Path.Combine(extractionDirectory, entry.FullName);
                            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                            entry.ExtractToFile(outPath, overwrite: true);
                            extractTask.Increment(1);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            return e.Message;
        }
        return null;
    }

    internal static string ConstructArchiveName(string? versionString, string rid, string suffix)
    {
        return versionString is null
            ? $"dotnet-sdk-{rid}{suffix}"
            : $"dotnet-sdk-{versionString}-{rid}{suffix}";
    }

    private string? ExtractSdkToDir(string extractedArchivePath, string destDir, IEnumerable<DotnetVersion> existingSdkVersions, Spectre.Console.ProgressTask? commitTask = null, List<string>? files = null)
    {
        // Ensure destination directory exists
        Directory.CreateDirectory(destDir);

        DotnetVersion? existingMuxerVersion = existingSdkVersions.Any() ? existingSdkVersions.Max() : (DotnetVersion?)null;
        DotnetVersion runtimeVersion = _install.FullySpecifiedVersion;

        try
        {
            CopyMuxer(existingMuxerVersion, runtimeVersion, extractedArchivePath, destDir);
            var fileList = files ?? Directory.EnumerateFileSystemEntries(extractedArchivePath, "*", SearchOption.AllDirectories).ToList();
            int processed = 0;
            foreach (var sourcePath in fileList)
            {
                var relativePath = Path.GetRelativePath(extractedArchivePath, sourcePath);
                var destPath = Path.Combine(destDir, relativePath);

                if (File.Exists(sourcePath))
                {
                    // Skip dotnet.exe
                    if (string.Equals(Path.GetFileName(sourcePath), DnupUtilities.GetDotnetExeName(), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    DnupUtilities.ForceReplaceFile(sourcePath, destPath);
                }
                else if (Directory.Exists(sourcePath))
                {
                    // Merge directories: create if not exists, do not delete anything in dest
                    Directory.CreateDirectory(destPath);
                }
                processed++;
                if (commitTask != null)
                {
                    commitTask.Value = processed;
                }
            }
        }
        catch (Exception e)
        {
            return e.Message;
        }
        return null;
    }

    private void CopyMuxer(DotnetVersion? existingMuxerVersion, DotnetVersion newRuntimeVersion, string archiveDir, string destDir)
    {
        // The "dotnet" exe (muxer) is special in two ways:
        // 1. It is shared between all SDKs, so it may be locked by another process.
        // 2. It should always be the newest version, so we don't want to overwrite it if the SDK
        //    we're installing is older than the one already installed.
        var muxerTargetPath = Path.Combine(destDir, DnupUtilities.GetDotnetExeName());

        if (existingMuxerVersion is not null && newRuntimeVersion.CompareTo(existingMuxerVersion) <= 0)
        {
            // The new SDK is older than the existing muxer, so we don't need to do anything.
            return;
        }

        // The new SDK is newer than the existing muxer, so we need to replace it.
        DnupUtilities.ForceReplaceFile(Path.Combine(archiveDir, DnupUtilities.GetDotnetExeName()), muxerTargetPath);
    }

    public void Commit()
    {
        Commit(GetExistingSdkVersions(_request.TargetDirectory));
    }

    public void Commit(IEnumerable<DotnetVersion> existingSdkVersions)
    {
        Spectre.Console.AnsiConsole.Progress()
            .Start(ctx =>
            {
                var files = Directory.EnumerateFileSystemEntries(scratchExtractionDirectory, "*", SearchOption.AllDirectories).ToList();
                var commitTask = ctx.AddTask($"Installing .NET SDK", autoStart: true);
                commitTask.MaxValue = files.Count > 0 ? files.Count : 1;
                ExtractSdkToDir(scratchExtractionDirectory, _request.TargetDirectory, existingSdkVersions, commitTask, files);
                commitTask.Value = commitTask.MaxValue;
            });
    }

    public void Dispose()
    {
        File.Delete(scratchExtractionDirectory);
        File.Delete(scratchDownloadDirectory);
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
            return Enumerable.Empty<DotnetVersion>();
        }
    }
}
