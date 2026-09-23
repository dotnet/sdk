// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Replaces and restores executables while the caller holds both update locks. Recovery never deletes artifacts.</summary>
/// <remarks>
/// One instance owns one transaction, including partial-failure recovery. The caller retains both locks
/// and exclusive ownership of these paths; uncooperative external modifications are not supported.
/// Recovery uses the retained backup and move state, never execution or inspection of a broken candidate.
/// </remarks>
internal sealed class SelfUpdateReplacement
{
    private readonly SelfUpdatePaths _paths;
    private readonly string _backupPath;
    private bool _mutationStarted;
    private bool _replacementCompleted;
    private bool _restored;

    public SelfUpdateReplacement(SelfUpdatePaths paths, string backupPath)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrEmpty(backupPath);
        _paths = paths;
        _backupPath = SelfUpdatePaths.ResolvePath(backupPath);
    }

    public void Replace()
        => Replace(static (source, destination, backup) => File.Replace(source, destination, backup, ignoreMetadataErrors: false));

    internal void Replace(Action<string, string, string> replaceFile)
    {
        ArgumentNullException.ThrowIfNull(replaceFile);
        if (_mutationStarted)
        {
            throw new InvalidOperationException("A replacement transaction cannot be reused.");
        }

        try
        {
            _paths.Validate();
            _paths.ValidateBackupPath(_backupPath);
            SelfUpdatePaths.RequireAbsent(_backupPath);
            PrepareFilesForReplacement();

            _mutationStarted = true;
            if (OperatingSystem.IsWindows())
            {
                replaceFile(_paths.StagedPath, _paths.InstalledPath, _backupPath);
            }
            else
            {
                File.CreateHardLink(_backupPath, _paths.InstalledPath);
                File.Move(_paths.StagedPath, _paths.InstalledPath, overwrite: true);
            }

            _replacementCompleted = true;
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            if (_mutationStarted)
            {
                try
                {
                    if (SelfUpdatePaths.Exists(_backupPath))
                    {
                        Rollback();
                    }

                    using var original = SelfUpdatePaths.OpenFile(_paths.InstalledPath);
                    if (!_restored && !SelfUpdatePaths.Exists(_paths.StagedPath))
                    {
                        throw new IOException("Replacement failed without a recoverable original executable.");
                    }
                }
                catch (Exception recoveryException) when (IsFileFailure(recoveryException) || recoveryException is DotnetInstallException)
                {
                    throw Failure("Replacement and recovery failed; reinstall dotnetup.",
                        new AggregateException(exception, recoveryException));
                }
            }

            throw Failure("Replacement failed; the original executable and any recovery artifacts were retained.", exception);
        }
    }

    private void PrepareFilesForReplacement()
    {
        using (var staged = SelfUpdatePaths.OpenFile(_paths.StagedPath, FileAccess.ReadWrite))
        {
            staged.Flush(flushToDisk: true);
        }

        // Backups and rejected candidates inherit these timestamps through renames/hard links.
        // Stamp before replacement so a metadata failure cannot interrupt recovery afterward.
        var updateTime = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(_paths.InstalledPath, updateTime);
        File.SetLastWriteTimeUtc(_paths.StagedPath, updateTime);
    }

    public void Rollback()
    {
        try
        {
            if (_restored)
            {
                return;
            }

            if (!_mutationStarted)
            {
                throw new IOException("This transaction has not begun replacement.");
            }

            _paths.ValidateLocation();
            _paths.ValidateBackupPath(_backupPath);
            using (SelfUpdatePaths.OpenFile(_backupPath))
            {
            }

            if (SelfUpdatePaths.Exists(_paths.InstalledPath))
            {
                using (SelfUpdatePaths.OpenFile(_paths.InstalledPath))
                {
                }

                // If the stage still exists, the rename did not complete. On Unix the backup
                // hard link may already exist; on Windows File.Replace can fail before switching.
                if (!_replacementCompleted && SelfUpdatePaths.Exists(_paths.StagedPath))
                {
                    _restored = true;
                    return;
                }

                if (OperatingSystem.IsWindows())
                {
                    var rejectedPath = _backupPath + ".rejected";
                    SelfUpdatePaths.RequireAbsent(rejectedPath);
                    File.Move(_paths.InstalledPath, rejectedPath, overwrite: false);
                }
            }

            if (OperatingSystem.IsWindows())
            {
                File.Move(_backupPath, _paths.InstalledPath, overwrite: false);
            }
            else
            {
                File.Move(_backupPath, _paths.InstalledPath, overwrite: true);
            }

            _restored = true;
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            throw Failure("Rollback failed; recovery artifacts were retained. Reinstall dotnetup if the canonical executable is unavailable.", exception);
        }
    }

    private static bool IsFileFailure(Exception exception) =>
        exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private DotnetInstallException Failure(string message, Exception exception) =>
        new(DotnetInstallErrorCode.InstallFailed,
            $"{message} Installed: '{_paths.InstalledPath}'. Staged: '{_paths.StagedPath}'. Backup: '{_backupPath}'. Rejected: '{_backupPath}.rejected'.", exception);
}