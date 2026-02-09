// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
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
    private readonly string _muxerName;
    private readonly string _muxerTargetPath;
    private readonly string _tempMuxerPath;
    private readonly string _existingMuxerBackupPath;
    private readonly bool _requireMuxerUpdate;

    private ReleaseVersion? _preExtractionHighestRuntimeVersion;
    private bool _hadExistingMuxer;
    private bool _extractedNewMuxer;
    private bool _movedExistingMuxer;

    public MuxerHandler(string targetDir, bool requireMuxerUpdate = false)
    {
        _targetDir = targetDir;
        _requireMuxerUpdate = requireMuxerUpdate;
        _muxerName = DotnetupUtilities.GetDotnetExeName();
        _muxerTargetPath = Path.Combine(targetDir, _muxerName);
        _tempMuxerPath = $"{_muxerTargetPath}.{Guid.NewGuid()}.new";
        _existingMuxerBackupPath = $"{_muxerTargetPath}.{Guid.NewGuid()}.old";
    }

    /// <summary>
    /// Gets the muxer entry name to detect during extraction.
    /// </summary>
    public string MuxerEntryName => _muxerName;

    /// <summary>
    /// Gets the path where the muxer should be extracted to during the main extraction pass.
    /// This is a temp path - the muxer will be moved to its final location after extraction.
    /// </summary>
    public string TempMuxerPath => _tempMuxerPath;

    /// <summary>
    /// Records the state before extraction begins.
    /// Call this before extracting the archive.
    /// </summary>
    public void RecordPreExtractionState()
    {
        _preExtractionHighestRuntimeVersion = GetLatestRuntimeVersionFromInstallRoot(_targetDir);
        _hadExistingMuxer = File.Exists(_muxerTargetPath);
    }

    /// <summary>
    /// Called by the extractor when the muxer entry is being extracted.
    /// Returns the path where the muxer should be written.
    /// </summary>
    public string GetMuxerExtractionPath()
    {
        _extractedNewMuxer = true;
        return _tempMuxerPath;
    }

    /// <summary>
    /// After extraction completes, determines if the muxer should be updated
    /// and moves/deletes the temp muxer accordingly.
    /// </summary>
    public void FinalizeAfterExtraction()
    {
        // If no muxer was extracted (e.g., WindowsDesktop), nothing to do
        if (!_extractedNewMuxer || !File.Exists(_tempMuxerPath))
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
                return;
            }
        }

        try
        {
            // Move the new muxer into place
            File.Move(_tempMuxerPath, _muxerTargetPath);

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
                return "it is currently in use by another process. Close all running .NET applications and try again";
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
