// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Rejects busy or stale invocations before installation state can be accessed.</summary>
internal static class SelfUpdateGate
{
    public static ScopedLockFile Enter(SelfUpdatePaths paths, string loadedIdentity)
    {
        ScopedLockFile? lease = null;
        try
        {
            paths.Validate();
            lease = ScopedLockFile.TryAcquireShared(paths.ActivityLockPath)
                ?? throw new DotnetInstallException(DotnetInstallErrorCode.DotnetupUpdateInProgress,
                    Strings.SelfUpdateInProgress + " " + SelfUpdateLockDiagnostics.Describe(paths.ActivityLockPath));
            if (!string.Equals(loadedIdentity, SelfUpdatePaths.ReadIdentity(paths.InstalledPath), StringComparison.Ordinal))
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