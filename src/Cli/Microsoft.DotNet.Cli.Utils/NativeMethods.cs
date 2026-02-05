// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils;

internal static partial class NativeMethods
{
#if NET
    internal static partial class Posix
    {
        [LibraryImport("libc", SetLastError = true)]
        internal static partial int kill(int pid, int sig);

        internal const int SIGINT = 2;
        internal const int SIGTERM = 15;
    }
#endif
}
