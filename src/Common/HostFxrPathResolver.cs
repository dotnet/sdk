// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        // Pick the highest version directory. Directory names frequently include
        // prerelease/build suffixes (e.g. "11.0.0-preview.6.26359.118"), which
        // Version.TryParse cannot handle, so parse the numeric core for comparison.
        string? latestFxr = getDirectories(fxrDir)
            .Select(path =>
            {
                string name = Path.GetFileName(path);
                int suffix = name.IndexOfAny(new[] { '-', '+' });
                bool isStable = suffix < 0;
                string core = isStable ? name : name.Substring(0, suffix);
                return new
                {
                    Path = path,
                    Name = name,
                    IsStable = isStable,
                    Version = Version.TryParse(core, out Version? version) ? version : null
                };
            })
            .Where(candidate => candidate.Version is not null)
            .OrderByDescending(candidate => candidate.Version)
            .ThenByDescending(candidate => candidate.IsStable)
            .ThenByDescending(candidate => candidate.Name, s_versionNameComparer)
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

    /// <summary>
    ///  Orders version directory names so that numeric segments compare numerically
    ///  (e.g. <c>preview.10</c> sorts after <c>preview.6</c>). A plain ordinal compare
    ///  would place <c>preview.6</c> after <c>preview.10</c> because <c>'6' &gt; '1'</c>.
    /// </summary>
    private static readonly IComparer<string> s_versionNameComparer =
        Comparer<string>.Create(CompareVersionNamesNumerically);

    private static int CompareVersionNamesNumerically(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int startA = i, startB = j;
                while (i < a.Length && char.IsDigit(a[i]))
                {
                    i++;
                }
                while (j < b.Length && char.IsDigit(b[j]))
                {
                    j++;
                }

                string numA = a.Substring(startA, i - startA).TrimStart('0');
                string numB = b.Substring(startB, j - startB).TrimStart('0');

                // Longer numeric run (ignoring leading zeros) is the larger number.
                if (numA.Length != numB.Length)
                {
                    return numA.Length - numB.Length;
                }

                int ordinal = string.CompareOrdinal(numA, numB);
                if (ordinal != 0)
                {
                    return ordinal;
                }
            }
            else
            {
                int cmp = char.ToLowerInvariant(a[i]).CompareTo(char.ToLowerInvariant(b[j]));
                if (cmp != 0)
                {
                    return cmp;
                }
                i++;
                j++;
            }
        }

        // Whichever string still has characters left is the greater one.
        return (a.Length - i) - (b.Length - j);
    }
}
