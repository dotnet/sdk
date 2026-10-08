// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>
/// Best-effort deletion of aged self-update backups in a caller-owned installation directory.
/// Only the self-update transaction runs cleanup, while it retains the update and activity locks.
/// </summary>
internal static class SelfUpdateCleanup
{
    private const int BackupRetentionDays = 7;
    private const int EntryBudget = 32;
    private const string RejectedSuffix = ".rejected";

    /// <summary>Runs cleanup with the caller's update lock, without acquiring or releasing it.</summary>
    public static void RunWithUpdateLock(string installedPath, Action<Exception>? onFailure = null)
    {
        Exception? firstFailure = null;
        try
        {
            installedPath = SelfUpdatePaths.ResolvePath(installedPath);
            var directory = new DirectoryInfo(Path.GetDirectoryName(installedPath)!);
            if (!IsPlainDirectoryPath(directory) || !IsPlainFile(installedPath))
            {
                return;
            }

            DeleteExpiredBackups(installedPath, directory, ref firstFailure);
        }
        catch (Exception exception)
        {
            firstFailure = exception;
        }

        if (firstFailure is not null)
        {
            try
            {
                onFailure?.Invoke(firstFailure);
            }
            catch (Exception)
            {
            }
        }
    }

    private static void DeleteExpiredBackups(string installedPath, DirectoryInfo directory, ref Exception? firstFailure)
    {
        var cutoff = DateTime.UtcNow.AddDays(-BackupRetentionDays);
        var prefix = Path.GetFileName(installedPath) + ".old.";
        using var entries = directory.EnumerateFileSystemInfos(prefix + "*", new EnumerationOptions
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

                if ((entry.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 ||
                    entry.LastWriteTimeUtc > cutoff)
                {
                    continue;
                }

                File.Delete(entry.FullName);
            }
            catch (Exception exception)
            {
                firstFailure ??= exception;
            }
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
}