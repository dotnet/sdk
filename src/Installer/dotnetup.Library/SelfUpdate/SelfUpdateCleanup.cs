// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>
/// Best-effort deletion of aged self-update backups in a caller-owned installation directory.
/// Cooperating updaters must retain the update lock and keep directory paths stable.
/// </summary>
internal static class SelfUpdateCleanup
{
    private const int EntryBudget = 32;

    public static void TryRun(string installedPath, string loadedIdentity)
    {
        try
        {
            installedPath = Path.GetFullPath(installedPath);
            var directory = new DirectoryInfo(Path.GetDirectoryName(installedPath)!);
            if (!IsPlainDirectoryPath(directory))
            {
                return;
            }

            var lockPath = Path.Combine(directory.FullName, "dotnetup.update.lock");
            if (!IsPlainLockPath(lockPath))
            {
                return;
            }

            using var updateLock = ScopedLockFile.TryAcquireExclusive(lockPath);
            if (updateLock is not null)
            {
                RunWithUpdateLock(installedPath, loadedIdentity);
            }
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Runs cleanup with the caller's update lock, without acquiring or releasing it.</summary>
    public static void RunWithUpdateLock(string installedPath, string loadedIdentity)
    {
        try
        {
            installedPath = Path.GetFullPath(installedPath);
            var directory = new DirectoryInfo(Path.GetDirectoryName(installedPath)!);
            if (!IsPlainDirectoryPath(directory) || !IsPlainFile(installedPath))
            {
                return;
            }

            using var canonical = new FileStream(installedPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (!string.Equals(DotnetupBuildIdentityReader.Read(canonical), loadedIdentity, StringComparison.Ordinal))
            {
                return;
            }

            var cutoff = DateTime.UtcNow.AddDays(-1);
            var prefix = Path.GetFileName(installedPath) + ".old.";
            using var entries = directory.EnumerateFileSystemInfos("*", new EnumerationOptions
            {
                RecurseSubdirectories = false,
                AttributesToSkip = 0,
                IgnoreInaccessible = true,
            }).GetEnumerator();

            for (var visited = 0; visited < EntryBudget && entries.MoveNext(); visited++)
            {
                try
                {
                    var entry = entries.Current;
                    if (!IsBackupName(entry.Name, prefix))
                    {
                        continue;
                    }

                    entry.Refresh();
                    if (!entry.Exists || (entry.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 ||
                        entry.LastWriteTimeUtc > cutoff)
                    {
                        continue;
                    }

                    File.Delete(entry.FullName);
                }
                catch (Exception)
                {
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private static bool IsBackupName(string name, string prefix)
    {
        if (!name.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var transaction = name.AsSpan(prefix.Length);
        if (transaction.EndsWith(".rejected", StringComparison.Ordinal))
        {
            transaction = transaction[..^9];
        }

        return transaction.Length == 32 && Guid.TryParseExact(transaction, "N", out _);
    }

    private static bool IsPlainDirectoryPath(DirectoryInfo directory)
    {
        for (DirectoryInfo? ancestor = directory; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (!ancestor.Exists || (ancestor.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPlainFile(string path)
        => (File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0;

    private static bool IsPlainLockPath(string path)
    {
        try
        {
            return IsPlainFile(path);
        }
        catch (FileNotFoundException)
        {
            return true;
        }
    }
}