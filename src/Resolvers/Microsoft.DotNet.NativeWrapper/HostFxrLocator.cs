// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.HostFxr;

namespace Microsoft.DotNet.NativeWrapper
{
    /// <summary>
    ///  NativeWrapper-facing entry point for locating the native <c>hostfxr</c> library.
    ///  The actual path-resolution logic lives in the source-shared
    ///  <see cref="HostFxrPathResolver"/> (under <c>src/Common</c>) so this resolver and the
    ///  Native AOT <c>dn</c> host stay in lockstep.
    /// </summary>
    internal static class HostFxrLocator
    {
        /// <summary>
        ///  Finds the highest-versioned <c>hostfxr</c> library under
        ///  <paramref name="dotnetRoot"/>/host/fxr, returning its full path or
        ///  <see cref="string.Empty"/> if it cannot be found.
        /// </summary>
        internal static string ResolveHostFxrPath(
            string? dotnetRoot,
            bool isWindows,
            bool isMacOS,
            Func<string, bool> directoryExists,
            Func<string, string[]> getDirectories,
            Func<string, bool> fileExists)
            => HostFxrPathResolver.ResolveHostFxrPath(
                dotnetRoot,
                isWindows,
                isMacOS,
                directoryExists,
                getDirectories,
                fileExists);

#if NET
        /// <summary>
        ///  Resolves the <c>hostfxr</c> path against the live filesystem, deriving the .NET
        ///  installation root from the current process (the running <c>dotnet</c> host).
        /// </summary>
        internal static string ResolveHostFxrPath()
            => ResolveHostFxrPath(
                EnvironmentProvider.GetDotnetExeDirectory(),
                isWindows: OperatingSystem.IsWindows(),
                isMacOS: OperatingSystem.IsMacOS(),
                directoryExists: Directory.Exists,
                getDirectories: Directory.GetDirectories,
                fileExists: File.Exists);
#endif
    }
}
