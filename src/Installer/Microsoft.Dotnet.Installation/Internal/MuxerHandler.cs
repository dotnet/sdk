// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Handles muxer (dotnet executable) replacement logic during archive extraction.
/// The muxer should only be replaced when installing a newer core runtime version.
///
/// Workflow:
/// 1. Before extraction: record the highest existing runtime version
/// 2. Extract everything, but redirect the muxer to a temp file
/// 3. After extraction: check if a higher runtime version now exists
/// 4. If yes, move the temp muxer to the real location; otherwise delete it
/// </summary>
internal class MuxerHandler
{
    private readonly string _targetDir;
    private readonly string _muxerTargetPath;
    private readonly string _tempMuxerPath;
    private readonly string _existingMuxerBackupPath;
    private readonly bool _requireMuxerUpdate;

    private readonly ReleaseVersion? _preExtractionHighestRuntimeVersion;
    private readonly bool _hadExistingMuxer;
    private bool _movedExistingMuxer;

    public MuxerHandler(string targetDir, bool requireMuxerUpdate = false)
    {
        _targetDir = targetDir;
        _requireMuxerUpdate = requireMuxerUpdate;
        var muxerName = DotnetupUtilities.GetDotnetExeName();
        _muxerTargetPath = Path.Combine(targetDir, muxerName);
        var guid = Guid.NewGuid();
        _tempMuxerPath = $"{_muxerTargetPath}.{guid}.new";
        _existingMuxerBackupPath = $"{_muxerTargetPath}.{guid}.old";

        _preExtractionHighestRuntimeVersion = GetLatestRuntimeVersionFromInstallRoot(_targetDir);
        _hadExistingMuxer = File.Exists(_muxerTargetPath);
    }

    /// <summary>
    /// Checks whether the muxer at the given install root is writable.
    /// Throws <see cref="InvalidOperationException"/> if it is locked by another process.
    /// Callers should invoke this while holding the installation-state mutex to
    /// avoid racing with other processes that modify the same hive.
    /// </summary>
    public static void EnsureMuxerIsWritable(string installRoot)
    {
        var muxerPath = Path.Combine(installRoot, DotnetupUtilities.GetDotnetExeName());
        if (!File.Exists(muxerPath))
        {
            return;
        }

        try
        {
            // Probe with FileShare.None to detect sharing/lock violations
            using var fs = new FileStream(muxerPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        catch (Exception ex) when (IsFileMoveBlockedException(ex))
        {
            string reason = GetMoveBlockedReason(ex);
            throw new InvalidOperationException(
                $"Cannot update dotnet executable at '{muxerPath}' - {reason}.", ex);
        }
    }

    /// <summary>
    /// Gets the muxer entry name to detect during extraction.
    /// </summary>
    public static string MuxerEntryName => DotnetupUtilities.GetDotnetExeName();

    /// <summary>
    /// Gets the path where the muxer should be extracted to during the main extraction pass.
    /// If there is no existing muxer, this returns the final target path directly.
    /// Otherwise, it returns a temp path so the muxer can be moved into place after extraction.
    /// </summary>
    public string TempMuxerPath => _hadExistingMuxer ? _tempMuxerPath : _muxerTargetPath;

    /// <summary>
    /// Set to true by the caller when the muxer entry has been extracted.
    /// </summary>
    public bool MuxerWasExtracted { get; set; }

    /// <summary>
    /// After extraction completes, determines if the muxer should be updated
    /// and moves/deletes the temp muxer accordingly.
    /// </summary>
    public void FinalizeAfterExtraction()
    {
        // If no muxer was extracted (e.g., WindowsDesktop), nothing to do
        if (!MuxerWasExtracted)
        {
            Activity.Current?.SetTag("muxer.action", "skipped_not_in_archive");
            return;
        }

        // If there was no existing muxer, the new muxer was extracted directly
        // to its final location - nothing more to do.
        if (!_hadExistingMuxer)
        {
            Activity.Current?.SetTag("muxer.action", "new_install");
            return;
        }

        // From here on, the muxer was extracted to a temp path and we need to
        // decide whether to replace the existing one.
        if (!File.Exists(_tempMuxerPath))
        {
            return;
        }

        var postExtractionHighestRuntimeVersion = GetLatestRuntimeVersionFromInstallRoot(_targetDir);

        // If no runtime exists after extraction, something is wrong - but keep the muxer
        if (postExtractionHighestRuntimeVersion == null)
        {
            // Clean up temp file since we can't determine what to do
            TryDeleteTempMuxer();
            return;
        }

        // Determine if we should update the muxer
        bool shouldUpdateMuxer;
        if (_preExtractionHighestRuntimeVersion == null)
        {
            // No runtime existed before - we need the muxer
            shouldUpdateMuxer = true;
        }
        else if (postExtractionHighestRuntimeVersion > _preExtractionHighestRuntimeVersion)
        {
            // A higher runtime version was installed - update the muxer
            shouldUpdateMuxer = true;
        }
        else
        {
            // Existing runtime is same or higher - keep existing muxer
            shouldUpdateMuxer = false;
        }

        if (!shouldUpdateMuxer)
        {
            TryDeleteTempMuxer();
            Activity.Current?.SetTag("muxer.action", "kept_existing");
            Activity.Current?.SetTag("muxer.existing_version", VersionSanitizer.Sanitize(_preExtractionHighestRuntimeVersion?.ToString()));
            Activity.Current?.SetTag("muxer.archive_version", VersionSanitizer.Sanitize(postExtractionHighestRuntimeVersion?.ToString()));
            return;
        }

        // Move the existing muxer out of the way if it exists
        if (_hadExistingMuxer)
        {
            try
            {
                File.Move(_muxerTargetPath, _existingMuxerBackupPath);
                _movedExistingMuxer = true;
            }
            catch (Exception ex) when (IsFileMoveBlockedException(ex))
            {
                TryDeleteTempMuxer();

                string reason = GetMoveBlockedReason(ex);

                if (_requireMuxerUpdate)
                {
                    throw new InvalidOperationException(
                        $"Cannot update dotnet executable at '{_muxerTargetPath}' - {reason}.", ex);
                }

                Console.Error.WriteLine(
                    $"Warning: Could not update dotnet executable at '{_muxerTargetPath}' - {reason}. " +
                    $"The existing muxer will be retained. This may cause issues if the new runtime requires a newer muxer.");
                Activity.Current?.SetTag("muxer.action", "blocked");
                Activity.Current?.SetTag("muxer.blocked_reason", reason);
                return;
            }
        }

        try
        {
            // Move the new muxer into place
            File.Move(_tempMuxerPath, _muxerTargetPath);

            Activity.Current?.SetTag("muxer.action", "updated");
            Activity.Current?.SetTag("muxer.previous_version", VersionSanitizer.Sanitize(_preExtractionHighestRuntimeVersion?.ToString()));
            Activity.Current?.SetTag("muxer.new_version", VersionSanitizer.Sanitize(postExtractionHighestRuntimeVersion?.ToString()));

            // Clean up the backup
            if (_movedExistingMuxer && File.Exists(_existingMuxerBackupPath))
            {
                try { File.Delete(_existingMuxerBackupPath); } catch { }
            }
        }
        catch
        {
            // Restore the original muxer if we moved it
            if (_movedExistingMuxer && File.Exists(_existingMuxerBackupPath) && !File.Exists(_muxerTargetPath))
            {
                try { File.Move(_existingMuxerBackupPath, _muxerTargetPath); } catch { }
            }
            throw;
        }
    }

    private void TryDeleteTempMuxer()
    {
        if (File.Exists(_tempMuxerPath))
        {
            try { File.Delete(_tempMuxerPath); } catch { }
        }
    }

    /// <summary>
    /// Gets the latest runtime version from the install root by checking the shared/Microsoft.NETCore.App directory.
    /// Uses <see cref="ReleaseVersion"/> for proper semver comparison, including pre-release labels.
    /// </summary>
    internal static ReleaseVersion? GetLatestRuntimeVersionFromInstallRoot(string installRoot)
    {
        var runtimePath = Path.Combine(installRoot, "shared", "Microsoft.NETCore.App");
        if (!Directory.Exists(runtimePath))
        {
            return null;
        }

        ReleaseVersion? highestVersion = null;
        foreach (var dir in Directory.GetDirectories(runtimePath))
        {
            var versionString = Path.GetFileName(dir);
            if (ReleaseVersion.TryParse(versionString, out ReleaseVersion? dirVersion))
            {
                if (highestVersion == null || dirVersion > highestVersion)
                {
                    highestVersion = dirVersion;
                }
            }
        }

        return highestVersion;
    }

    /// <summary>
    /// Determines whether the exception represents a condition that blocks moving the muxer file.
    /// This includes file-in-use (sharing/lock violations), permission errors, and other I/O failures.
    /// </summary>
    private static bool IsFileMoveBlockedException(Exception ex)
    {
        return ex is IOException || ex is UnauthorizedAccessException;
    }

    /// <summary>
    /// Returns a human-readable reason for why the muxer file move was blocked.
    /// </summary>
    private static string GetMoveBlockedReason(Exception ex)
    {
        if (ex is IOException ioEx && OperatingSystem.IsWindows())
        {
            const int ERROR_SHARING_VIOLATION = unchecked((int)0x80070020);
            const int ERROR_LOCK_VIOLATION = unchecked((int)0x80070021);

            if (ioEx.HResult == ERROR_SHARING_VIOLATION || ioEx.HResult == ERROR_LOCK_VIOLATION)
            {
                string pidInfo = DotnetupUtilities.GetDotnetProcessPidInfo();
                return $"it is currently in use by another process.{pidInfo} Close all running .NET applications and try again";
            }

            return $"an I/O error occurred (HRESULT 0x{ioEx.HResult:X8}): {ioEx.Message}";
        }

        if (ex is UnauthorizedAccessException)
        {
            return $"access was denied. Check file permissions and ensure you have write access to the installation directory. Details: {ex.Message}";
        }

        if (ex is IOException)
        {
            return $"an I/O error occurred: {ex.Message}";
        }

        return ex.Message;
    }

}
