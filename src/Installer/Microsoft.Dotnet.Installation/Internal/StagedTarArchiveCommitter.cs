// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

internal static class StagedTarArchiveCommitter
{
    private const int DirectoryMoveAttempts = 5;

    public static void Commit(string stagedInstallRoot, TarExtractionContext context)
    {
        Directory.CreateDirectory(context.TargetDirectory);

        foreach (string stagedInstallRootSubdirectory in Directory.EnumerateDirectories(stagedInstallRoot))
        {
            string subdirectoryName = Path.GetFileName(stagedInstallRootSubdirectory);
            if (SubcomponentResolver.TryGetDepth(subdirectoryName, out int depth))
            {
                CommitKnownSubdirectories(stagedInstallRoot, stagedInstallRootSubdirectory, subdirectoryName, depth, context);
            }
            else
            {
                // metadata and swidtag are decidedly not known subcomponents, but they should be copied
                string destination = Path.Combine(context.TargetDirectory, subdirectoryName);
                MergeDirectory(stagedInstallRootSubdirectory, destination);
            }
        }

        foreach (string stagedFile in Directory.EnumerateFiles(stagedInstallRoot))
        {
            CommitRootFile(stagedFile, context);
        }
    }

    private static void CommitKnownSubdirectories(
        string stagedInstallRoot,
        string stagedInstallRootSubdirectory,
        string subdirectoryName,
        int depth,
        TarExtractionContext context)
    {
        string[] stagedSubdirectory = [.. EnumerateDirectoriesAtDepth(stagedInstallRootSubdirectory, depth - 1)];
        foreach (string subdirectory in stagedSubdirectory)
        {
            string relativePath = Path.GetRelativePath(stagedInstallRoot, subdirectory);
            string archiveEntryName  = relativePath.Replace(Path.DirectorySeparatorChar, '/') + "/";
            context.OnEntryExtracted?.Invoke(archiveEntryName);

            string destination = Path.Combine(context.TargetDirectory, relativePath);
            if (context.ShouldSkipEntry?.Invoke(archiveEntryName) == true || Directory.Exists(destination))
            {
                // Remove skipped content before the remaining staging tree is merged. Leaving it
                // here would overwrite the existing live subcomponent during the merge below.
                Directory.Delete(subdirectory, recursive: true);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            MoveDirectoryWithRetry(subdirectory, destination);
        }

        if (Directory.Exists(stagedInstallRootSubdirectory))
        {
            MergeDirectory(stagedInstallRootSubdirectory, Path.Combine(context.TargetDirectory, subdirectoryName));
        }
    }

    private static void CommitRootFile(string stagedFile, TarExtractionContext context)
    {
        string fileName = Path.GetFileName(stagedFile);
        if (context.MuxerHandler is not null &&
            string.Equals(fileName, MuxerHandler.MuxerEntryName, StringComparison.Ordinal))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(context.MuxerHandler.TempMuxerPath)!);
            File.Move(stagedFile, context.MuxerHandler.TempMuxerPath, overwrite: true);
            context.MuxerHandler.MuxerWasExtracted = true;
            return;
        }

        File.Move(stagedFile, Path.Combine(context.TargetDirectory, fileName), overwrite: true);
        context.OnEntryExtracted?.Invoke(fileName);
    }

    private static IEnumerable<string> EnumerateDirectoriesAtDepth(string root, int remainingDepth)
    {
        if (remainingDepth == 0)
        {
            yield return root;
            yield break;
        }

        foreach (string child in Directory.EnumerateDirectories(root))
        {
            foreach (string result in EnumerateDirectoriesAtDepth(child, remainingDepth - 1))
            {
                yield return result;
            }
        }
    }

    private static void MergeDirectory(string source, string destination)
    {
        if (!Directory.Exists(destination))
        {
            MoveDirectoryWithRetry(source, destination);
            return;
        }

        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Move(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            MergeDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }

        Directory.Delete(source);
    }

    private static void MoveDirectoryWithRetry(string source, string destination)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Move(source, destination);
                return;
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException &&
                attempt < DirectoryMoveAttempts &&
                Directory.Exists(source) &&
                !Directory.Exists(destination))
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(100 * attempt));
            }
        }
    }
}
