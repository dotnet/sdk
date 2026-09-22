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
    private const int BackupRetentionDays = 7;
    private const int EntryBudget = 32;
    private const string RejectedSuffix = ".rejected";

    public static void TryRun(string installedPath, string loadedVersion)
    {
        try
        {
            installedPath = SelfUpdatePaths.ResolvePath(installedPath);
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
                RunWithUpdateLock(installedPath, loadedVersion);
            }
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Runs cleanup with the caller's update lock, without acquiring or releasing it.</summary>
    public static void RunWithUpdateLock(string installedPath, string loadedVersion)
    {
        try
        {
            // Normalize independently because callers that already own the update lock bypass TryRun.
            installedPath = SelfUpdatePaths.ResolvePath(installedPath);
            var directory = new DirectoryInfo(Path.GetDirectoryName(installedPath)!);
            // TryRun validates before acquiring the lock; repeat because the filesystem objects may have changed meanwhile.
            if (!IsPlainDirectoryPath(directory) || !IsPlainFile(installedPath))
            {
                return;
            }

            var cutoff = DateTime.UtcNow.AddDays(-BackupRetentionDays);
            var prefix = Path.GetFileName(installedPath) + ".old.";
            using var entries = directory.EnumerateFileSystemInfos(prefix + "*", new EnumerationOptions
            {
                RecurseSubdirectories = false,
                AttributesToSkip = 0,
                IgnoreInaccessible = true,
            }).GetEnumerator();

            var versionChecked = false;
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

                    if (!versionChecked)
                    {
                        // --version bypasses cleanup and both locks. Keep the parent's update lock
                        // through comparison and deletion; compare build metadata too.
                        if (!string.Equals(SelfUpdateVerifier.ReadVersion(installedPath), loadedVersion, StringComparison.Ordinal))
                        {
                            return;
                        }

                        versionChecked = true;
                    }

                    File.Delete(entry.FullName);
                }
                catch (DotnetInstallException)
                {
                    return;
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
        if (transaction.EndsWith(RejectedSuffix, StringComparison.Ordinal))
        {
            transaction = transaction[..^RejectedSuffix.Length];
        }

        return SelfUpdatePaths.IsBackupIdentifier(transaction);
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