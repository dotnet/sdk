// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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
        // Download the archive to a user protected (wrx) random folder in temp

        // string archiveName = ConstructArchiveName(versionString: null, Utilities.CurrentRID, Utilities.ZipSuffix);
        // string archivePath = Path.Combine(scratchDownloadDirectory, archiveName);

        // Download to scratchDownloadDirectory

        // Verify the hash and or signature of the archive
        VerifyArchive(scratchDownloadDirectory);

        // Extract to a temporary directory for the final replacement later.
        ExtractArchive(scratchDownloadDirectory, scratchExtractionDirectory);
    }

    /**
    Returns a string if the archive is valid within SDL specification, false otherwise.
    */
    private void VerifyArchive(string archivePath)
    {
        if (archivePath != null) // replace this with actual verification logic once its implemented.
        {
            throw new InvalidOperationException("Archive verification failed.");
        }
    }

    /**
    Extracts the specified archive to the given extraction directory.
    The archive will be decompressed if necessary.
    Expects either a .tar.gz, .tar, or .zip archive.
    */
    private string? ExtractArchive(string archivePath, string extractionDirectory)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var needsDecompression = archivePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
            string decompressedPath = archivePath;

            try
            {
                // Run gzip decompression iff .gz is at the end of the archive file, which is true for .NET archives
                if (needsDecompression)
                {
                    decompressedPath = Path.Combine(Path.GetDirectoryName(archivePath) ?? Directory.CreateTempSubdirectory().FullName, "decompressed.tar");
                    using FileStream originalFileStream = File.OpenRead(archivePath);
                    using FileStream decompressedFileStream = File.Create(decompressedPath);
                    using GZipStream decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress);
                    decompressionStream.CopyTo(decompressedFileStream);
                }

                // Use System.Formats.Tar for .NET 7+
                TarFile.ExtractToDirectory(decompressedPath, extractionDirectory, overwriteFiles: true);

                // Clean up temporary decompressed file
                if (needsDecompression && File.Exists(decompressedPath))
                {
                    File.Delete(decompressedPath);
                }
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
        else
        {
            try
            {
                ZipFile.ExtractToDirectory(archivePath, extractionDirectory, overwriteFiles: true);
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
        return null;
    }

    internal static string ConstructArchiveName(string? versionString, string rid, string suffix)
    {
        return versionString is null
            ? $"dotnet-sdk-{rid}{suffix}"
            : $"dotnet-sdk-{versionString}-{rid}{suffix}";
    }


    private string? ExtractSdkToDir(string extractedArchivePath, string destDir, IEnumerable<DotnetVersion> existingSdkVersions)
    {
        // Ensure destination directory exists
        Directory.CreateDirectory(destDir);

        DotnetVersion? existingMuxerVersion = existingSdkVersions.Any() ? existingSdkVersions.Max() : (DotnetVersion?)null;
        DotnetVersion runtimeVersion = _install.FullySpecifiedVersion;

        try
        {
            CopyMuxer(existingMuxerVersion, runtimeVersion, extractedArchivePath, destDir);

            foreach (var sourcePath in Directory.EnumerateFileSystemEntries(extractedArchivePath, "*", SearchOption.AllDirectories))
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
        Commit(existingSdkVersions: Enumerable.Empty<DotnetVersion>()); // todo impl this
    }

    public void Commit(IEnumerable<DotnetVersion> existingSdkVersions)
    {
        ExtractSdkToDir(scratchExtractionDirectory, _request.TargetDirectory, existingSdkVersions);
    }

    public void Dispose()
    {
        File.Delete(scratchExtractionDirectory);
        File.Delete(scratchDownloadDirectory);
    }
}
