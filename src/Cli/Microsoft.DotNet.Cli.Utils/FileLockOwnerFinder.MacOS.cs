// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

namespace Microsoft.DotNet.Cli.Utils;

internal static partial class FileLockOwnerFinder
{
    /// <summary>
    /// Avoids attributing a macOS advisory lock when the platform cannot identify its actual owner.
    /// </summary>
    /// <remarks>
    /// <c>proc_listpidspath</c> identifies processes with the path open, but does not prove ownership of the BSD
    /// <c>flock</c> used by <see cref="FileShare.None"/>. The generic contention message is safer than misattribution.
    /// </remarks>
    private static FileLockOwner? TryGetMacOSFileLockOwner(string filePath) => null;
}

#endif
