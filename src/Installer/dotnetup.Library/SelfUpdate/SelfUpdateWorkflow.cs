// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Stages and smoke-tests one release while coordinating replacement and recovery.</summary>
internal class SelfUpdateWorkflow
{
    private readonly SelfUpdatePaths _paths;
    private readonly string _loadedVersionMetadata;
    private readonly Func<ResolvedDownload> _resolve;
    private readonly Action<ResolvedDownload, string> _download;
    private readonly SelfUpdateCoordinator _coordinator;

    public SelfUpdateWorkflow(SelfUpdatePaths paths, string loadedVersionMetadata, Func<ResolvedDownload> resolve,
        Action<ResolvedDownload, string> download, SelfUpdateCoordinator? coordinator = null)
    {
        _paths = paths;
        _loadedVersionMetadata = loadedVersionMetadata;
        _resolve = resolve;
        _download = download;
        _coordinator = coordinator ?? new SelfUpdateCoordinator();
    }

    public string? Execute(Action<IDisposable> retainUntilExit)
    {
        ArgumentNullException.ThrowIfNull(retainUntilExit);
        SelfUpdateLockLease? locks = null;
        try
        {
            _paths.ValidateLocation();
            var release = _resolve();

            try
            {
                if (!IsUpdateAvailable(SelfUpdatePaths.ReadVersionMetadata(_paths.InstalledPath), release))
                {
                    return null;
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
            }

            locks = AcquireLocks();
            retainUntilExit(locks);
            locks = null;

            _paths.Validate();
            var originalMetadata = SelfUpdatePaths.ReadVersionMetadata(_paths.InstalledPath);
            if (!IsUpdateAvailable(originalMetadata, release))
            {
                return null;
            }

            StageRelease(release);
            var replacement = new SelfUpdateReplacement(_paths, _paths.CreateBackupPath(), originalMetadata);
            replacement.Replace();
            VerifyOrRestore(replacement);
            return release.Version.ToString();
        }
        catch (InvalidDataException exception)
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.DotnetupIdentityUnavailable, Strings.SelfUpdateIdentityUnavailable, exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.InstallFailed, Strings.SelfUpdateAccessFailed, exception);
        }
        finally
        {
            locks?.Dispose();
        }
    }

    protected virtual void Verify(string installedPath)
        => SelfUpdateVerifier.Verify(installedPath, TimeSpan.FromSeconds(15));

    private static bool IsUpdateAvailable(string installedMetadata, ResolvedDownload release)
    {
        var separator = installedMetadata.IndexOf('|');
        if (separator <= 0 ||
            !ReleaseVersion.TryParse(installedMetadata[..separator], out var installedVersion) ||
            !string.Equals(installedMetadata[(separator + 1)..], release.Rid, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The installed dotnetup version metadata is invalid.");
        }

        return !HasSameSemanticChannel(installedVersion, release.Version) ||
            release.Version.CompareTo(installedVersion) > 0;
    }

    private static bool HasSameSemanticChannel(ReleaseVersion left, ReleaseVersion right)
        => string.Equals(GetSemanticChannel(left), GetSemanticChannel(right), StringComparison.OrdinalIgnoreCase);

    private static string GetSemanticChannel(ReleaseVersion version)
    {
        if (string.IsNullOrEmpty(version.Prerelease))
        {
            return "";
        }

        var separator = version.Prerelease.IndexOf('.');
        return separator < 0 ? version.Prerelease : version.Prerelease[..separator];
    }

    private SelfUpdateLockLease AcquireLocks()
    {
        try
        {
            return _coordinator.Acquire(_paths.UpdateLockPath, _paths.ActivityLockPath);
        }
        catch (SelfUpdateLockTimeoutException exception)
        {
            var updateBusy = exception.LockKind == SelfUpdateLockKind.Update;
            throw new DotnetInstallException(updateBusy
                    ? DotnetInstallErrorCode.DotnetupBusyWithUpdateOrCleanup
                    : DotnetInstallErrorCode.DotnetupBusyWithAnotherCommand,
                updateBusy ? Strings.SelfUpdateBusyUpdate : Strings.SelfUpdateBusyCommand, exception);
        }
    }

    private void StageRelease(ResolvedDownload release)
    {
        ClearStagingFile(_paths.StagedPath);
        ClearStagingFile(_paths.StagedPath + ".download");
        SelfUpdateCleanup.RunWithUpdateLock(_paths.InstalledPath, _loadedVersionMetadata);
        _download(release, _paths.StagedPath);
        if (SelfUpdatePaths.ReadVersionMetadata(_paths.StagedPath) != DotnetupVersionMetadataReader.Format(release.Version.ToString(), release.Rid))
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.DotnetupIdentityUnavailable, Strings.SelfUpdateStagedIdentityMismatch);
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_paths.StagedPath, File.GetUnixFileMode(_paths.InstalledPath));
        }
    }

    private void VerifyOrRestore(SelfUpdateReplacement replacement)
    {
        try
        {
            Verify(_paths.InstalledPath);
        }
        catch (Exception verificationFailure)
        {
            try
            {
                replacement.Rollback();
            }
            catch (Exception rollbackFailure)
            {
                throw new DotnetInstallException(DotnetInstallErrorCode.DotnetupRollbackFailed, Strings.SelfUpdateRollbackFailed,
                    new AggregateException(verificationFailure, rollbackFailure));
            }

            throw new DotnetInstallException(DotnetInstallErrorCode.DotnetupVerificationFailed,
                Strings.SelfUpdateVerificationFailed, verificationFailure);
        }
    }

    private static void ClearStagingFile(string path)
    {
        if (SelfUpdatePaths.Exists(path))
        {
            using (SelfUpdatePaths.OpenFile(path, FileAccess.ReadWrite))
            {
            }

            File.Delete(path);
        }
    }
}
