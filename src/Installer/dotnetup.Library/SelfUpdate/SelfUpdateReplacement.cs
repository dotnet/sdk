// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Replaces and restores executables while the caller holds both update locks. Recovery never deletes artifacts.</summary>
/// <remarks>
/// Reuse the same paths instance for rollback so an occupied canonical path can be checked against the recorded replacement.
/// A missing canonical path can also be recovered without that evidence if the backup matches the original identity.
/// </remarks>
internal static class SelfUpdateReplacement
{
    private static readonly ConditionalWeakTable<SelfUpdatePaths, SelfUpdateReplacementState> s_transactions = new();

    public static void Replace(SelfUpdatePaths paths, string backupPath)
        => Replace(paths, backupPath, static (source, destination, backup) => File.Replace(source, destination, backup, ignoreMetadataErrors: false));

    internal static void Replace(SelfUpdatePaths paths, string backupPath, Action<string, string, string> replaceFile)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(replaceFile);
        var mutationStarted = false;
        SelfUpdateReplacementState? transaction = null;
        try
        {
            using var directory = SelfUpdateFile.PinDirectory(paths.DirectoryPath);
            paths.Validate();
            paths.ValidateBackupPath(backupPath);
            SelfUpdateFile.RequireAbsent(backupPath);
            var originalIdentity = SelfUpdatePaths.ReadIdentity(paths.InstalledPath);
            var replacementIdentity = SelfUpdatePaths.ReadIdentity(paths.StagedPath);
            using (var staged = SelfUpdateFile.Open(paths.StagedPath, FileAccess.ReadWrite))
            {
                staged.Flush(flushToDisk: true);
            }

            transaction = new SelfUpdateReplacementState(backupPath, originalIdentity, replacementIdentity);
            _ = s_transactions.Remove(paths);
            s_transactions.Add(paths, transaction);
            mutationStarted = true;
            if (OperatingSystem.IsWindows())
            {
                replaceFile(paths.StagedPath, paths.InstalledPath, backupPath);
            }
            else
            {
                SelfUpdateFile.CreateBackupUnix(directory, paths.InstalledPath, backupPath);
                SelfUpdateFile.MoveUnix(directory, paths.StagedPath, paths.InstalledPath);
            }
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            if (mutationStarted && transaction is not null)
            {
                try
                {
                    if (SelfUpdateFile.Exists(backupPath))
                    {
                        Rollback(paths, backupPath, transaction.OriginalIdentity);
                    }

                    RequireIdentity(paths.InstalledPath, transaction.OriginalIdentity);
                }
                catch (Exception recoveryException) when (IsFileFailure(recoveryException) || recoveryException is DotnetInstallException)
                {
                    throw Failure(paths, backupPath, "Replacement and recovery failed; reinstall dotnetup.",
                        new AggregateException(exception, recoveryException));
                }
            }

            throw Failure(paths, backupPath, "Replacement failed; the original executable and any recovery artifacts were retained.", exception);
        }
    }

    public static void Rollback(SelfUpdatePaths paths, string backupPath, string originalIdentity)
    {
        ArgumentNullException.ThrowIfNull(paths);
        try
        {
            using var directory = SelfUpdateFile.PinDirectory(paths.DirectoryPath);
            paths.ValidateLocation();
            paths.ValidateBackupPath(backupPath);
            RequireIdentity(backupPath, originalIdentity);
            if (SelfUpdateFile.Exists(paths.InstalledPath))
            {
                var canonicalIdentity = SelfUpdatePaths.ReadIdentity(paths.InstalledPath);
                if (string.Equals(canonicalIdentity, originalIdentity, StringComparison.Ordinal))
                {
                    return;
                }

                if (!s_transactions.TryGetValue(paths, out var transaction) ||
                    !string.Equals(transaction.BackupPath, backupPath, StringComparison.Ordinal) ||
                    !string.Equals(transaction.OriginalIdentity, originalIdentity, StringComparison.Ordinal) ||
                    !string.Equals(transaction.ReplacementIdentity, canonicalIdentity, StringComparison.Ordinal))
                {
                    throw new IOException("The canonical executable is not the known replacement; rollback will not overwrite it.");
                }

                if (OperatingSystem.IsWindows())
                {
                    var rejectedPath = backupPath + ".rejected";
                    SelfUpdateFile.RequireAbsent(rejectedPath);
                    File.Move(paths.InstalledPath, rejectedPath, overwrite: false);
                }
            }

            if (OperatingSystem.IsWindows())
            {
                File.Move(backupPath, paths.InstalledPath, overwrite: false);
            }
            else
            {
                SelfUpdateFile.MoveUnix(directory, backupPath, paths.InstalledPath);
            }

            RequireIdentity(paths.InstalledPath, originalIdentity);
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            throw Failure(paths, backupPath, "Rollback failed; recovery artifacts were retained. Reinstall dotnetup if the canonical executable is unavailable.", exception);
        }
    }

    private static void RequireIdentity(string path, string identity)
    {
        if (!string.Equals(SelfUpdatePaths.ReadIdentity(path), identity, StringComparison.Ordinal))
        {
            throw new IOException($"The build identity of '{path}' does not match the expected transaction identity.");
        }
    }

    private static bool IsFileFailure(Exception exception) =>
        exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private static DotnetInstallException Failure(SelfUpdatePaths paths, string backupPath, string message, Exception exception) =>
        new(DotnetInstallErrorCode.InstallFailed,
            $"{message} Installed: '{paths.InstalledPath}'. Staged: '{paths.StagedPath}'. Backup: '{backupPath}'. Rejected: '{backupPath}.rejected'.", exception);
}