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

        // Match the native host's get_latest_fxr behavior: consider every valid
        // semantic version, including prerelease versions, and select the highest.
        // SDK and runtime roll-forward settings apply after hostfxr is loaded and
        // do not affect hostfxr selection.
        string? latestFxr = getDirectories(fxrDir)
            .Select(path =>
            {
                HostFxrVersion.TryParse(Path.GetFileName(path), out HostFxrVersion? version);
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

    // Mirrors the SemVer parsing and precedence used by the native host's
    // c_fx_ver_parse and c_fx_ver_compare functions.
    private sealed class HostFxrVersion : IComparable<HostFxrVersion>
    {
        private readonly uint _major;
        private readonly uint _minor;
        private readonly uint _patch;
        private readonly string[] _prereleaseIdentifiers;

        private HostFxrVersion(uint major, uint minor, uint patch, string[] prereleaseIdentifiers)
        {
            _major = major;
            _minor = minor;
            _patch = patch;
            _prereleaseIdentifiers = prereleaseIdentifiers;
        }

        public int CompareTo(HostFxrVersion? other)
        {
            if (other is null)
            {
                return 1;
            }

            int comparison = _major.CompareTo(other._major);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = _minor.CompareTo(other._minor);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = _patch.CompareTo(other._patch);
            if (comparison != 0)
            {
                return comparison;
            }

            bool isPrerelease = _prereleaseIdentifiers.Length > 0;
            bool otherIsPrerelease = other._prereleaseIdentifiers.Length > 0;
            if (isPrerelease != otherIsPrerelease)
            {
                return isPrerelease ? -1 : 1;
            }

            int identifierCount = Math.Min(_prereleaseIdentifiers.Length, other._prereleaseIdentifiers.Length);
            for (int i = 0; i < identifierCount; i++)
            {
                comparison = ComparePrereleaseIdentifiers(
                    _prereleaseIdentifiers[i],
                    other._prereleaseIdentifiers[i]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return _prereleaseIdentifiers.Length.CompareTo(other._prereleaseIdentifiers.Length);
        }

        internal static bool TryParse(string value, out HostFxrVersion? version)
        {
            version = null;

            int buildSeparator = value.IndexOf('+');
            if (buildSeparator >= 0)
            {
                if (!AreValidIdentifiers(value.Substring(buildSeparator + 1), allowLeadingZeros: true))
                {
                    return false;
                }

                value = value.Substring(0, buildSeparator);
            }

            string[] prereleaseIdentifiers = Array.Empty<string>();
            int prereleaseSeparator = value.IndexOf('-');
            if (prereleaseSeparator >= 0)
            {
                string prerelease = value.Substring(prereleaseSeparator + 1);
                if (!AreValidIdentifiers(prerelease, allowLeadingZeros: false))
                {
                    return false;
                }

                prereleaseIdentifiers = prerelease.Split('.');
                value = value.Substring(0, prereleaseSeparator);
            }

            string[] coreIdentifiers = value.Split('.');
            if (coreIdentifiers.Length != 3
                || !TryParseCoreIdentifier(coreIdentifiers[0], out uint major)
                || !TryParseCoreIdentifier(coreIdentifiers[1], out uint minor)
                || !TryParseCoreIdentifier(coreIdentifiers[2], out uint patch))
            {
                return false;
            }

            version = new HostFxrVersion(major, minor, patch, prereleaseIdentifiers);
            return true;
        }

        private static bool TryParseCoreIdentifier(string value, out uint result)
        {
            result = 0;
            return IsNumericIdentifier(value)
                && (value.Length == 1 || value[0] != '0')
                && uint.TryParse(value, out result);
        }

        private static bool AreValidIdentifiers(string value, bool allowLeadingZeros)
        {
            string[] identifiers = value.Split('.');
            foreach (string identifier in identifiers)
            {
                if (identifier.Length == 0)
                {
                    return false;
                }

                bool isNumeric = true;
                foreach (char character in identifier)
                {
                    if (!IsValidIdentifierCharacter(character))
                    {
                        return false;
                    }

                    isNumeric &= character is >= '0' and <= '9';
                }

                if (!allowLeadingZeros && isNumeric && identifier.Length > 1 && identifier[0] == '0')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidIdentifierCharacter(char character) =>
            character is >= '0' and <= '9'
                or >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or '-';

        private static bool IsNumericIdentifier(string value)
        {
            if (value.Length == 0)
            {
                return false;
            }

            foreach (char character in value)
            {
                if (character is < '0' or > '9')
                {
                    return false;
                }
            }

            return true;
        }

        private static int ComparePrereleaseIdentifiers(string left, string right)
        {
            bool leftIsNumeric = IsNumericIdentifier(left);
            bool rightIsNumeric = IsNumericIdentifier(right);

            if (leftIsNumeric && rightIsNumeric)
            {
                int comparison = left.Length.CompareTo(right.Length);
                return comparison != 0 ? comparison : string.CompareOrdinal(left, right);
            }

            if (leftIsNumeric != rightIsNumeric)
            {
                return leftIsNumeric ? -1 : 1;
            }

            return string.CompareOrdinal(left, right);
        }
    }
}
