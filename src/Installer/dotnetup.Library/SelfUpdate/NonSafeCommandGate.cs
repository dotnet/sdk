// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Rejects busy or stale non-safe invocations before installation state can be accessed.</summary>
internal static class NonSafeCommandGate
{
    public static ScopedLockFile Enter(SelfUpdatePaths paths, string loadedVersion)
    {
        ScopedLockFile? lease = null;
        try
        {
            paths.Validate();
            lease = ScopedLockFile.TryAcquireShared(paths.ActivityLockPath)
                ?? throw new DotnetInstallException(DotnetInstallErrorCode.DotnetupUpdateInProgress,
                    Strings.SelfUpdateInProgress);
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
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
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