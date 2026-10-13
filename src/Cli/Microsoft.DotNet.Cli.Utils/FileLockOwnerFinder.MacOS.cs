// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

namespace Microsoft.DotNet.Cli.Utils;

internal static partial class FileLockOwnerFinder
{
    private static FileLockOwner? TryGetMacOSFileLockOwner(string filePath)
    {
        _ = filePath;

        // macOS has no /proc filesystem. The native implementation would enumerate processes with
        // proc_listpids, enumerate descriptors with proc_pidinfo(PROC_PIDLISTFDS), then resolve vnode
        // paths with proc_pidfdinfo(PROC_PIDFDVNODEPATHINFO) from libproc.
        return null;
    }
}

#endif
