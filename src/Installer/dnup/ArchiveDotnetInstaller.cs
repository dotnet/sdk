// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class ArchiveDotnetInstaller : IDotnetInstaller, IDisposable
{
    private string scratchDownloadDirectory;
    private string scratchExtractionDirectory;

    public ArchiveDotnetInstaller(DotnetInstall version)
    {
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

        // Extract to a temporary directory for the final replacement later.
        // ExtractArchive(scratchDownloadDirectory, scratchExtractionDirectory);
    }

    /**
    private async Task<string?> ExtractArchive(string archivePath, IFileSystem extractionDirectory)
    {
        // TODO: Ensure this fails if the dir already exists as that's a security issue
        extractionDirectory.CreateDirectory(tempExtractDir);

        using var tempRealPath = new DirectoryResource(extractionDirectory.ConvertPathToInternal(tempExtractDir));
        if (Utilities.CurrentRID.OS != OSPlatform.Windows)
        {
            // TODO: See if this works if 'tar' is unavailable
            var procResult = await ProcUtil.RunWithOutput("tar", $"-xzf \"{archivePath}\" -C \"{tempRealPath.Path}\"");
            if (procResult.ExitCode != 0)
            {
                return procResult.Error;
            }
        }
        else
        {
            try
            {
                ZipFile.ExtractToDirectory(archivePath, tempRealPath.Path, overwriteFiles: true);
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
    */

    internal static string ConstructArchiveName(string? versionString, string rid, string suffix)
    {
        return versionString is null
            ? $"dotnet-sdk-{rid}{suffix}"
            : $"dotnet-sdk-{versionString}-{rid}{suffix}";
    }

    /**
    public static async Task<string?> ExtractSdkToDir(
        DotnetVersion? existingMuxerVersion,
        DotnetVersion runtimeVersion,
        string archivePath,
        IFileSystem tempFs,
        IFileSystem destFs,
        string destDir)
    {
        destFs.CreateDirectory(destDir);

        try
        {
            // We want to copy over all the files from the extraction directory to the target
            // directory, with one exception: the top-level "dotnet exe" (muxer). That has special logic.
            CopyMuxer(existingMuxerVersion, runtimeVersion, tempFs, tempExtractDir, destFs, destDir);

            var extractFullName = tempExtractDir.FullName;
            foreach (var dir in tempFs.EnumerateDirectories(tempExtractDir))
            {
                destFs.CreateDirectory(Path.Combine(destDir, dir.GetName()));
                foreach (var fsItem in tempFs.EnumerateItems(dir, SearchOption.AllDirectories))
                {
                    var relativePath = fsItem.Path.FullName[extractFullName.Length..].TrimStart('/');
                    var destPath = Path.Combine(destDir, relativePath);

                    if (fsItem.IsDirectory)
                    {
                        destFs.CreateDirectory(destPath);
                    }
                    else
                    {
                        ForceReplaceFile(tempFs, fsItem.Path, destFs, destPath);
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
    */

    /**
    private static void CopyMuxer(
        DotnetVersion? existingMuxerVersion,
        DotnetVersion newRuntimeVersion,
        IFileSystem tempFs,
        string tempExtractDir,
        IFileSystem destFs,
        string destDir)
    {   //The "dotnet" exe (muxer) is special in two ways:
        // 1. It is shared between all SDKs, so it may be locked by another process.
        // 2. It should always be the newest version, so we don't want to overwrite it if the SDK
        //    we're installing is older than the one already installed.
        //
        var muxerTargetPath = Path.Combine(destDir, DotnetExeName);

        if (newRuntimeVersion.CompareSortOrderTo(existingMuxerVersion) <= 0)
        {
            // The new SDK is older than the existing muxer, so we don't need to do anything.
            return;
        }

        // The new SDK is newer than the existing muxer, so we need to replace it.
        ForceReplaceFile(tempFs, Path.Combine(tempExtractDir, DotnetExeName), destFs, muxerTargetPath);
    }
    */

    public void Commit()
    {
        //ExtractSdkToDir();
    }

    public void Dispose()
    {
        File.Delete(scratchExtractionDirectory);
        File.Delete(scratchDownloadDirectory);
    }
}
