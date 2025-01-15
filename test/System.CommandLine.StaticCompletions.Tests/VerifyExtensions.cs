// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.StaticCompletions.Shells;
using System.Runtime.CompilerServices;

namespace System.CommandLine.StaticCompletions.Tests;

public static class VerifyExtensions
{
    public static async Task Verify(CliCommand command, IShellProvider provider, [CallerFilePath] string sourceFile = "")
    {
        // Can't use sourceFile directly because in CI the file may be rooted at a different location than the compile-time location
        // We do have the source code available, just at a different root, so we can use that compute
        var completions = provider.GenerateCompletions(command);
        var settings = new VerifySettings();
        var repoRoot = GetRepoRoot();
        if (repoRoot is null)
        {
            throw new DirectoryNotFoundException("Must be in a git directory");
        }

        var runtimeSnapshotDir = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "snapshots", provider.ArgumentName));
        var closestExistingDirectory = GetClosestExistingDirectory(runtimeSnapshotDir);
        if (!runtimeSnapshotDir.Exists)
        {
            throw new DirectoryNotFoundException($"The directory ({runtimeSnapshotDir}) containing the source file ({sourceFile}) does not exist.\nVerify is going to try to recreate the directory and that won't work in CI.\nThe closest existing directory is ({closestExistingDirectory}). The current directory is ({Environment.CurrentDirectory}). The repo root is ({repoRoot})");
        }
        settings.UseDirectory(runtimeSnapshotDir.FullName);
        await Verifier.Verify(target: completions, extension: provider.Extension, settings: settings, sourceFile: sourceFile);
    }

    private static DirectoryInfo? GetClosestExistingDirectory(DirectoryInfo? path)
    {
        while (path is not null && !path.Exists)
        {
            path = path.Parent;
        }
        return path;
    }

    private static DirectoryInfo? GetRepoRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var gitPath = directory.GetFileSystemInfos(".git").FirstOrDefault();
            if (gitPath is FileSystemInfo gitData && gitData.Exists)
            {
                // Found the repo root, which should either have a .git folder or, if the repo
                // is part of a Git worktree, a .git file.
                return directory;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
