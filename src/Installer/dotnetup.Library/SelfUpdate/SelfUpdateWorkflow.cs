// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Stages and smoke-tests one release while coordinating replacement and recovery.</summary>
internal class SelfUpdateWorkflow
{
    private readonly SelfUpdatePaths _paths;
    private readonly string _loadedVersion;
    private readonly Func<ResolvedDownload> _resolve;
    private readonly Action<ResolvedDownload, string> _download;
    private readonly SelfUpdateCoordinator _coordinator;

    public SelfUpdateWorkflow(SelfUpdatePaths paths, string loadedVersion, Func<ResolvedDownload> resolve,
        Action<ResolvedDownload, string> download, SelfUpdateCoordinator? coordinator = null)
    {
        _paths = paths;
        _loadedVersion = loadedVersion;
        _resolve = resolve;
        _download = download;
        _coordinator = coordinator ?? new SelfUpdateCoordinator();
    }

    public string? Execute(Action<IDisposable> retainUntilExit)
    {
        var result = ExecuteWithResult(retainUntilExit);
        return result.WasUpdated ? result.AvailableVersion.ToString() : null;
    }

    public SelfUpdateResult ExecuteWithResult(Action<IDisposable> retainUntilExit)
    {
        ArgumentNullException.ThrowIfNull(retainUntilExit);
        SelfUpdateLockLease? locks = null;
        try
        {
            _paths.ValidateLocation();
            var release = _resolve();

            try
            {
                var installedVersion = GetInstalledVersion();
                if (!IsUpdateAvailable(installedVersion, release.Version))
                {
                    return new SelfUpdateResult(installedVersion, release.Version, WasUpdated: false);
                }
            }
            catch (DotnetInstallException exception) when (exception.ErrorCode == DotnetInstallErrorCode.DotnetupIdentityUnavailable)
            {
            }

            locks = AcquireLocks();
            retainUntilExit(locks);
            locks = null;

            _paths.Validate();
            var originalVersion = GetInstalledVersion();
            if (!IsUpdateAvailable(originalVersion, release.Version))
            {
                return new SelfUpdateResult(originalVersion, release.Version, WasUpdated: false);
            }

            StageRelease(release);
            var replacement = new SelfUpdateReplacement(_paths, _paths.CreateBackupPath());
            replacement.Replace();
            VerifyOrRestore(replacement, release.Version.ToString());
            return new SelfUpdateResult(originalVersion, release.Version, WasUpdated: true);
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

    protected virtual void Verify(string installedPath, string expectedVersion)
    {
        if (!string.Equals(SelfUpdateVerifier.ReadVersion(installedPath), expectedVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The updated dotnetup version does not match the expected release.");
        }
    }

    private ReleaseVersion GetInstalledVersion()
    {
        try
        {
            return ReleaseVersion.Parse(SelfUpdateVerifier.ReadVersion(_paths.InstalledPath));
        }
        catch (DotnetInstallException exception)
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.DotnetupIdentityUnavailable,
                Strings.SelfUpdateIdentityUnavailable, exception);
        }
    }

    private static bool IsUpdateAvailable(ReleaseVersion installedVersion, ReleaseVersion availableVersion)
        => !HasSameSemanticChannel(installedVersion, availableVersion) ||
            availableVersion.CompareTo(installedVersion) > 0;

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
        SelfUpdateCleanup.RunWithUpdateLock(_paths.InstalledPath, _loadedVersion);
        _download(release, _paths.StagedPath);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_paths.StagedPath, File.GetUnixFileMode(_paths.InstalledPath));
        }
    }

    private void VerifyOrRestore(SelfUpdateReplacement replacement, string expectedVersion)
    {
        try
        {
            Verify(_paths.InstalledPath, expectedVersion);
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
