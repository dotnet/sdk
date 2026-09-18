// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Replaces and restores executables while the caller holds both update locks. Recovery never deletes artifacts.</summary>
/// <remarks>
/// One instance owns the paths and identity evidence for one update, including partial-failure recovery.
/// A missing canonical path can also be recovered without that evidence if the backup matches the original identity.
/// </remarks>
internal sealed class SelfUpdateReplacement
{
    private readonly SelfUpdatePaths _paths;
    private readonly string _backupPath;
    private readonly string _originalIdentity;
    private string? _replacementIdentity;

    public SelfUpdateReplacement(SelfUpdatePaths paths, string backupPath, string originalIdentity)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrEmpty(backupPath);
        ArgumentException.ThrowIfNullOrEmpty(originalIdentity);
        _paths = paths;
        _backupPath = backupPath;
        _originalIdentity = originalIdentity;
    }

    public void Replace()
        => Replace(static (source, destination, backup) => File.Replace(source, destination, backup, ignoreMetadataErrors: false));

    internal void Replace(Action<string, string, string> replaceFile)
    {
        ArgumentNullException.ThrowIfNull(replaceFile);
        var mutationStarted = false;
        try
        {
            _paths.Validate();
            _paths.ValidateBackupPath(_backupPath);
            SelfUpdatePaths.RequireAbsent(_backupPath);
            RequireIdentity(_paths.InstalledPath, _originalIdentity);
            var replacementIdentity = SelfUpdatePaths.ReadIdentity(_paths.StagedPath);
            using (var staged = SelfUpdatePaths.OpenFile(_paths.StagedPath, FileAccess.ReadWrite))
            {
                staged.Flush(flushToDisk: true);
            }

            // Backups and rejected candidates inherit these timestamps through renames/hard links.
            // Stamp before replacement so a metadata failure cannot interrupt recovery afterward.
            var updateTime = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(_paths.InstalledPath, updateTime);
            File.SetLastWriteTimeUtc(_paths.StagedPath, updateTime);

            _replacementIdentity = replacementIdentity;
            mutationStarted = true;
            if (OperatingSystem.IsWindows())
            {
                replaceFile(_paths.StagedPath, _paths.InstalledPath, _backupPath);
            }
            else
            {
                File.CreateHardLink(_backupPath, _paths.InstalledPath);
                File.Move(_paths.StagedPath, _paths.InstalledPath, overwrite: true);
            }
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            if (mutationStarted)
            {
                try
                {
                    if (SelfUpdatePaths.Exists(_backupPath))
                    {
                        Rollback();
                    }

                    RequireIdentity(_paths.InstalledPath, _originalIdentity);
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

    public void Rollback()
    {
        try
        {
            _paths.ValidateLocation();
            _paths.ValidateBackupPath(_backupPath);
            RequireIdentity(_backupPath, _originalIdentity);
            if (SelfUpdatePaths.Exists(_paths.InstalledPath))
            {
                var canonicalIdentity = SelfUpdatePaths.ReadIdentity(_paths.InstalledPath);
                if (string.Equals(canonicalIdentity, _originalIdentity, StringComparison.Ordinal))
                {
                    return;
                }

                if (!string.Equals(_replacementIdentity, canonicalIdentity, StringComparison.Ordinal))
                {
                    throw new IOException("The canonical executable is not the known replacement; rollback will not overwrite it.");
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

            RequireIdentity(_paths.InstalledPath, _originalIdentity);
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            throw Failure("Rollback failed; recovery artifacts were retained. Reinstall dotnetup if the canonical executable is unavailable.", exception);
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

    private DotnetInstallException Failure(string message, Exception exception) =>
        new(DotnetInstallErrorCode.InstallFailed,
            $"{message} Installed: '{_paths.InstalledPath}'. Staged: '{_paths.StagedPath}'. Backup: '{_backupPath}'. Rejected: '{_backupPath}.rejected'.", exception);
}