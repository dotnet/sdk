// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>
/// Rejects busy or stale non-safe invocations before installation state can be accessed.
/// Location, coordination-file, executable-access, and version-query failures are translated at
/// the operation that failed.
/// </summary>
internal static class NonSafeCommandGate
{
    public static ScopedLockFile Enter(SelfUpdatePaths paths, string loadedVersion)
    {
        ScopedLockFile? lease = null;
        try
        {
            // Name-agnostic: renamed executables may run ordinary commands; only self-update needs the canonical name.
            ValidateDirectory(paths);
            lease = AcquireActivityLock(paths);
            ValidateExecutable(paths);
            string installedVersion = ReadInstalledVersion(paths.InstalledPath);

            if (!string.Equals(loadedVersion, installedVersion, StringComparison.Ordinal))
            {
                throw new DotnetInstallException(DotnetInstallErrorCode.DotnetupExecutableChanged,
                    Strings.SelfUpdateExecutableChanged);
            }

            var acquired = lease;
            lease = null;
            return acquired;
        }
        finally
        {
            lease?.Dispose();
        }
    }

    private static void ValidateDirectory(SelfUpdatePaths paths)
    {
        try
        {
            SelfUpdatePaths.ValidateDirectory(paths.DirectoryPath);
        }
        catch (SelfUpdateLocationException exception)
        {
            throw exception.ToInstallException();
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.PermissionDenied,
                string.Format(CultureInfo.CurrentCulture, Strings.SelfUpdateDirectoryAccessDenied, paths.DirectoryPath), exception);
        }
        catch (IOException exception)
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.InstallFailed,
                string.Format(CultureInfo.CurrentCulture, Strings.SelfUpdateDirectoryUnavailable, paths.DirectoryPath), exception);
        }
    }

    private static ScopedLockFile AcquireActivityLock(SelfUpdatePaths paths)
    {
        try
        {
            return ScopedLockFile.TryAcquireShared(paths.ActivityLockPath)
                ?? throw new DotnetInstallException(DotnetInstallErrorCode.DotnetupUpdateInProgress,
                    Strings.SelfUpdateInProgress);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.PermissionDenied,
                string.Format(CultureInfo.CurrentCulture, Strings.SelfUpdateActivityLockAccessDenied, paths.ActivityLockPath), exception);
        }
        catch (IOException exception)
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.InstallFailed,
                string.Format(CultureInfo.CurrentCulture, Strings.SelfUpdateActivityLockUnavailable, paths.ActivityLockPath), exception);
        }
    }

    private static void ValidateExecutable(SelfUpdatePaths paths)
    {
        try
        {
            paths.ValidateExecutable();
        }
        catch (SelfUpdateLocationException exception)
        {
            throw exception.ToInstallException();
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.PermissionDenied,
                string.Format(CultureInfo.CurrentCulture, Strings.SelfUpdateExecutableAccessDenied, paths.InstalledPath), exception);
        }
        catch (IOException exception)
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.DotnetupIdentityUnavailable,
                string.Format(CultureInfo.CurrentCulture, Strings.SelfUpdateExecutableUnavailable, paths.InstalledPath), exception);
        }
    }

    private static string ReadInstalledVersion(string installedPath)
    {
        try
        {
            return SelfUpdateVerifier.ReadInstalledVersion(installedPath);
        }
        catch (DotnetInstallException exception)
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.DotnetupIdentityUnavailable,
                Strings.SelfUpdateIdentityUnavailable, exception);
        }
    }
}