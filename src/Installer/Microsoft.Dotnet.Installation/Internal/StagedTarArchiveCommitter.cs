// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

internal static class StagedTarArchiveCommitter
{
    private const int DirectoryMoveAttempts = 5;

    public static void Commit(string stagingDirectory, TarExtractionContext context)
    {
        Directory.CreateDirectory(context.TargetDirectory);

        foreach (string topLevelDirectory in Directory.EnumerateDirectories(stagingDirectory))
        {
            string topLevelName = Path.GetFileName(topLevelDirectory);
            if (!SubcomponentResolver.TryGetDepth(topLevelName, out int depth))
            {
                continue;
            }

            CommitKnownSubcomponents(stagingDirectory, topLevelDirectory, topLevelName, depth, context);
        }

        foreach (string stagedFile in Directory.EnumerateFiles(stagingDirectory))
        {
            CommitRootFile(stagedFile, context);
        }

        foreach (string stagedDirectory in Directory.EnumerateDirectories(stagingDirectory))
        {
            string destination = Path.Combine(context.TargetDirectory, Path.GetFileName(stagedDirectory));
            MergeDirectory(stagedDirectory, destination);
        }
    }

    private static void CommitKnownSubcomponents(
        string stagingDirectory,
        string topLevelDirectory,
        string topLevelName,
        int depth,
        TarExtractionContext context)
    {
        string[] stagedSubcomponents = [.. EnumerateDirectoriesAtDepth(topLevelDirectory, depth - 1)];
        foreach (string stagedSubcomponent in stagedSubcomponents)
        {
            string relativePath = Path.GetRelativePath(stagingDirectory, stagedSubcomponent);
            string archivePath = relativePath.Replace(Path.DirectorySeparatorChar, '/') + "/";
            context.OnEntryExtracted?.Invoke(archivePath);

            string destination = Path.Combine(context.TargetDirectory, relativePath);
            if (context.ShouldSkipEntry?.Invoke(archivePath) == true || Directory.Exists(destination))
            {
                // Remove skipped content before the remaining staging tree is merged. Leaving it
                // here would overwrite the existing live subcomponent during the merge below.
                Directory.Delete(stagedSubcomponent, recursive: true);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            MoveDirectoryWithRetry(stagedSubcomponent, destination);
        }

        if (Directory.Exists(topLevelDirectory))
        {
            MergeDirectory(topLevelDirectory, Path.Combine(context.TargetDirectory, topLevelName));
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
