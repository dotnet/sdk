// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Versioning;

namespace Microsoft.DotNet.HostFxr;

/// <summary>
///  Locates the native <c>hostfxr</c> library underneath a .NET installation root.
///  This is used as a fallback when the host does not publish the <c>HOSTFXR_PATH</c>
///  runtime property (for example when the SDK is launched via <c>dotnet exec dotnet.dll</c>
///  rather than as a first-class SDK command, which is how the <c>dnx</c> script works).
///  Dependencies are injected so the pure path-resolution logic can be unit tested.
///  <para>
///   This file is source-shared (via <c>&lt;Compile Include&gt;</c>) into both the
///   <c>Microsoft.DotNet.NativeWrapper</c> resolver and the Native AOT <c>dn</c> host so
///   the two stay in lockstep; a prior divergence between separate copies is what caused
///   https://github.com/dotnet/sdk/issues/55238 to be fixed in only one place.
///  </para>
/// </summary>
internal static class HostFxrPathResolver
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
    {
        if (string.IsNullOrEmpty(dotnetRoot))
        {
            return string.Empty;
        }

        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        if (!directoryExists(fxrDir))
        {
            return string.Empty;
        }

        // Match the native host's get_latest_fxr behavior: consider every valid
        // semantic version, including prerelease versions, and select the highest.
        // SDK and runtime roll-forward settings apply after hostfxr is loaded and
        // do not affect hostfxr selection.
        string? latestFxr = getDirectories(fxrDir)
            .Select(path =>
            {
                SemanticVersion.TryParse(Path.GetFileName(path), out SemanticVersion? version);
                return new
                {
                    Path = path,
                    Version = version
                };
            })
            .Where(candidate => candidate.Version is not null)
            .OrderByDescending(candidate => candidate.Version)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();

        if (latestFxr is null)
        {
            return string.Empty;
        }

        string hostfxrName = isWindows
            ? "hostfxr.dll"
            : isMacOS
                ? "libhostfxr.dylib"
                : "libhostfxr.so";

        string hostfxrPath = Path.Combine(latestFxr, hostfxrName);
        return fileExists(hostfxrPath) ? hostfxrPath : string.Empty;
    }
}
