// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

internal sealed class WindowsNativeTarArchiveExtractor : ITarArchiveExtractor
{
    private const string TarExecutable = "tar.exe";
    private const int DirectoryMoveAttempts = 5;

    private readonly ITarArchiveExtractor _fallbackExtractor;
    private readonly INativeTarProcessRunner _processRunner;

    public WindowsNativeTarArchiveExtractor(
        ITarArchiveExtractor fallbackExtractor,
        INativeTarProcessRunner? processRunner = null)
    {
        _fallbackExtractor = fallbackExtractor;
        _processRunner = processRunner ?? new NativeTarProcessRunner();
    }

    public void Extract(TarExtractionContext context)
    {
        string stagingDirectory = CreateStagingDirectoryPath(context.TargetDirectory);
        Exception? nativeFailure = null;

        try
        {
            Directory.CreateDirectory(stagingDirectory);

            NativeTarProcessResult result = _processRunner.Run(
                TarExecutable,
                ["-xzf", context.ArchivePath, "-C", stagingDirectory]);
            if (result.StartFailure is not null)
            {
                nativeFailure = new InvalidOperationException(
                    $"Failed to start native TAR executable '{TarExecutable}': {result.StartFailure.Message}",
                    result.StartFailure);
                ReportFallback(nativeFailure.Message);
                TryDeleteDirectory(stagingDirectory);
                ExtractWithFallback(context, nativeFailure);
                return;
            }

            if (result.ExitCode != 0)
            {
                nativeFailure = new InvalidOperationException(CreateFailureMessage(
                    TarExecutable,
                    result.ExitCode!.Value,
                    result.StandardError,
                    context.ArchivePath,
                    stagingDirectory));
                ReportFallback(nativeFailure.Message);
                TryDeleteDirectory(stagingDirectory);
                ExtractWithFallback(context, nativeFailure);
                return;
            }

            CommitStagedArchive(stagingDirectory, context);
        }
        finally
        {
            TryDeleteDirectory(stagingDirectory);
        }
    }

    private void ExtractWithFallback(TarExtractionContext context, Exception nativeFailure)
    {
        try
        {
            _fallbackExtractor.Extract(context);
        }
        catch (Exception fallbackFailure)
        {
            throw new AggregateException(
                "Both Windows native TAR extraction and the .NET TAR fallback failed.",
                nativeFailure,
                fallbackFailure);
        }
    }

    private static void CommitStagedArchive(string stagingDirectory, TarExtractionContext context)
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
                TryDeleteDirectory(stagedSubcomponent);
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

    private static string CreateStagingDirectoryPath(string targetDirectory)
    {
        string fullTargetPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDirectory));
        string? parentDirectory = Directory.GetParent(fullTargetPath)?.FullName
            ?? Path.GetPathRoot(fullTargetPath);
        if (string.IsNullOrEmpty(parentDirectory))
        {
            throw new IOException($"Could not determine a staging location for '{targetDirectory}'.");
        }

        string targetName = Path.GetFileName(fullTargetPath);
        return Path.Combine(parentDirectory, $".{targetName}.dotnetup-staging-{Guid.NewGuid():N}");
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static void ReportFallback(string reason)
        => Console.Error.WriteLine($"Warning: {reason} Falling back to the .NET TAR extractor.");

    private static string CreateFailureMessage(
        string executable,
        int exitCode,
        string standardError,
        string archivePath,
        string destinationPath)
    {
        string details = string.IsNullOrWhiteSpace(standardError)
            ? string.Empty
            : $" Error output: {standardError.Trim()}";
        return $"Native TAR executable '{executable}' exited with code {exitCode} while extracting " +
            $"'{archivePath}' to '{destinationPath}'.{details}";
    }
}
