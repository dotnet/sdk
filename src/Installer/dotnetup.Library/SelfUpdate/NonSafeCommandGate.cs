// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>
/// Rejects busy or stale non-safe invocations before installation state can be accessed.
/// Unsupported locations (links or reparse points) and access-denied directories are reported as
/// specific user errors rather than as an unavailable executable identity.
/// </summary>
internal static class NonSafeCommandGate
{
    public static ScopedLockFile Enter(SelfUpdatePaths paths, string loadedVersion)
    {
        ScopedLockFile? lease = null;
        try
        {
            // Name-agnostic: renamed executables may run ordinary commands; only self-update needs the canonical name.
            SelfUpdatePaths.ValidateDirectory(paths.DirectoryPath);
            lease = ScopedLockFile.TryAcquireShared(paths.ActivityLockPath)
                ?? throw new DotnetInstallException(DotnetInstallErrorCode.DotnetupUpdateInProgress,
                    Strings.SelfUpdateInProgress);
            paths.ValidateExecutable();
            string installedVersion;
            try
            {
                installedVersion = SelfUpdateVerifier.ReadVersion(paths.InstalledPath);
            }
            catch (DotnetInstallException exception)
            {
                throw new DotnetInstallException(DotnetInstallErrorCode.DotnetupIdentityUnavailable,
                    Strings.SelfUpdateIdentityUnavailable, exception);
            }

            if (!string.Equals(loadedVersion, installedVersion, StringComparison.Ordinal))
            {
                throw new DotnetInstallException(DotnetInstallErrorCode.DotnetupExecutableChanged,
                    Strings.SelfUpdateExecutableChanged);
            }

            var acquired = lease;
            lease = null;
            return acquired;
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
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.DotnetupIdentityUnavailable,
                Strings.SelfUpdateIdentityUnavailable, exception);
        }
        finally
        {
            lease?.Dispose();
        }
    }
}