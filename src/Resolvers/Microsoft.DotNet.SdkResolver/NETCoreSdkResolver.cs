// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.DotNet.MSBuildSdkResolver;
using Microsoft.DotNet.NativeWrapper;

namespace Microsoft.DotNet.DotNetSdkResolver
{

    //  Thread safety note:
    //  This class is used by the MSBuild SDK resolvers, which can be called on multiple threads.
    public class NETCoreSdkResolver
    {
        private readonly Func<string, string?> _getEnvironmentVariable;
        private readonly VSSettings _vsSettings;

        // Caches of minimum versions, compatible SDKs are static to benefit multiple IDE evaluations.
        private static readonly ConcurrentDictionary<string, Version> s_minimumMSBuildVersions
            = new();

        private static readonly ConcurrentDictionary<CompatibleSdkKey, CompatibleSdkValue> s_compatibleSdks
            = new();

        public NETCoreSdkResolver()
            : this(Environment.GetEnvironmentVariable, VSSettings.Ambient)
        {
        }

        public NETCoreSdkResolver(Func<string, string?> getEnvironmentVariable, VSSettings vsSettings)
        {
            _getEnvironmentVariable = getEnvironmentVariable;
            _vsSettings = vsSettings;
        }

        private sealed class CompatibleSdkKey : IEquatable<CompatibleSdkKey>
        {
            public readonly string? DotnetExeDirectory;
            public readonly Version MSBuildVersion;

            public CompatibleSdkKey(string? dotnetExeDirectory, Version msbuildVersion)
            {
                DotnetExeDirectory = dotnetExeDirectory;
                MSBuildVersion = msbuildVersion;
            }

            public bool Equals(CompatibleSdkKey? other)
            {
                return other != null
                    && DotnetExeDirectory == other.DotnetExeDirectory
                    && MSBuildVersion == other.MSBuildVersion;
            }

            public override bool Equals(object? obj)
            {
                return Equals(obj as CompatibleSdkValue);
            }

            public override int GetHashCode()
            {
                int h1 = DotnetExeDirectory!.GetHashCode();
                int h2 = MSBuildVersion.GetHashCode();
                return unchecked(((h1 << 5) + h1) ^ h2);
            }
        }

        private sealed class CompatibleSdkValue
        {
            public readonly string? MostRecentCompatible;
            public readonly string? MostRecentCompatibleNonPreview;

            public CompatibleSdkValue(string? mostRecentCompatible, string? mostRecentCompatibleNonPreview)
            {
                MostRecentCompatible = mostRecentCompatible;
                MostRecentCompatibleNonPreview = mostRecentCompatibleNonPreview;
            }
        }

        private enum SdkRollForwardPolicy
        {
            Unsupported,
            Disable,
            Patch,
            Feature,
            Minor,
            Major,
            LatestPatch,
            LatestFeature,
            LatestMinor,
            LatestMajor,
        }

        private sealed class GlobalJsonSdkSettings
        {
            public FXVersion? RequestedVersion { get; }
            public SdkRollForwardPolicy RollForward { get; }
            public bool AllowPrerelease { get; }
            public IReadOnlyList<string?> SearchPaths { get; }

            public GlobalJsonSdkSettings(
                FXVersion? requestedVersion,
                SdkRollForwardPolicy rollForward,
                bool allowPrerelease,
                IReadOnlyList<string?> searchPaths)
            {
                RequestedVersion = requestedVersion;
                RollForward = rollForward;
                AllowPrerelease = allowPrerelease;
                SearchPaths = searchPaths;
            }
        }

        private string? GetMostCompatibleSdk(string? dotnetExeDirectory, Version msbuildVersion, int minimumSdkMajorVersion = 0)
        {
            CompatibleSdkValue sdks = GetMostCompatibleSdks(dotnetExeDirectory, msbuildVersion, minimumSdkMajorVersion);
            if (_vsSettings.DisallowPrerelease())
            {
                return sdks.MostRecentCompatibleNonPreview;
            }

            return sdks.MostRecentCompatible;
        }

        private CompatibleSdkValue GetMostCompatibleSdks(string? dotnetExeDirectory, Version msbuildVersion, int minimumSdkMajorVersion)
        {
            return s_compatibleSdks.GetOrAdd(
                new CompatibleSdkKey(dotnetExeDirectory, msbuildVersion),
                key =>
                {
                    string? mostRecent = null;
                    string? mostRecentNonPreview = null;

                    string[] availableSdks = NETCoreSdkResolverNativeWrapper.GetAvailableSdks(key.DotnetExeDirectory)!;
                    for (int i = availableSdks.Length - 1; i >= 0; i--)
                    {
                        string netcoreSdkDir = availableSdks[i];
                        string netcoreSdkVersion = new DirectoryInfo(netcoreSdkDir).Name;
                        Version minimumMSBuildVersion = GetMinimumMSBuildVersion(netcoreSdkDir);

                        if (key.MSBuildVersion < minimumMSBuildVersion)
                        {
                            continue;
                        }

                        if (minimumSdkMajorVersion != 0 && int.TryParse(netcoreSdkVersion.Split('.')[0], out int sdkMajorVersion) && sdkMajorVersion < minimumSdkMajorVersion)
                        {
                            continue;
                        }

                        if (mostRecent == null)
                        {
                            mostRecent = netcoreSdkDir;
                        }

                        if (netcoreSdkVersion.IndexOf('-') < 0)
                        {
                            mostRecentNonPreview = netcoreSdkDir;
                            break;
                        }
                    }

                    return new CompatibleSdkValue(mostRecent, mostRecentNonPreview);
                });
        }

        private string? GetMostCompatibleSdkForGlobalJson(
            SdkResolutionResult result,
            string? dotnetExeDirectory,
            Version msbuildVersion)
        {
            if (result.GlobalJsonPath is null
                || !TryReadGlobalJsonSdkSettings(result.GlobalJsonPath, result.RequestedVersion, dotnetExeDirectory, out GlobalJsonSdkSettings? settings)
                || settings is null)
            {
                return null;
            }

            foreach (string? searchPath in settings.SearchPaths)
            {
                string? compatibleSdk = GetMostCompatibleSdkForGlobalJsonSearchPath(searchPath, msbuildVersion, settings);
                if (compatibleSdk is not null)
                {
                    return compatibleSdk;
                }
            }

            return null;
        }

        private string? GetMostCompatibleSdkForGlobalJsonSearchPath(
            string? dotnetRoot,
            Version msbuildVersion,
            GlobalJsonSdkSettings settings)
        {
            if (dotnetRoot is null)
            {
                return null;
            }

            string[] availableSdks = GetAvailableSdkDirectories(dotnetRoot);

            if (settings.RequestedVersion is not null
                && settings.RollForward is SdkRollForwardPolicy.Disable or SdkRollForwardPolicy.Patch)
            {
                string? exactMatch = availableSdks.FirstOrDefault(
                    sdk => string.Equals(
                        new DirectoryInfo(sdk).Name,
                        settings.RequestedVersion.ToString(),
                        StringComparison.OrdinalIgnoreCase));

                if (exactMatch is not null && msbuildVersion >= GetMinimumMSBuildVersion(exactMatch))
                {
                    return exactMatch;
                }
            }

            if (settings.RollForward == SdkRollForwardPolicy.Disable)
            {
                return null;
            }

            string? bestMatch = null;
            FXVersion? bestVersion = null;

            foreach (string sdkDirectory in availableSdks)
            {
                string sdkVersionString = new DirectoryInfo(sdkDirectory).Name;
                if (!FXVersion.TryParse(sdkVersionString, out FXVersion? sdkVersion)
                    || sdkVersion is null
                    || !MatchesGlobalJsonPolicy(sdkVersion, settings)
                    || msbuildVersion < GetMinimumMSBuildVersion(sdkDirectory))
                {
                    continue;
                }

                if (bestVersion is null || IsBetterMatch(sdkVersion, bestVersion, settings))
                {
                    bestMatch = sdkDirectory;
                    bestVersion = sdkVersion;
                }
            }

            return bestMatch;
        }

        private static string[] GetAvailableSdkDirectories(string dotnetRoot)
        {
            try
            {
                string sdkRoot = Path.Combine(dotnetRoot, "sdk");
                return Directory.Exists(sdkRoot)
                    ? Directory.GetDirectories(sdkRoot)
                        .Where(sdk => File.Exists(Path.Combine(sdk, "dotnet.dll")))
                        .ToArray()
                    : [];
            }
            catch (IOException)
            {
                return [];
            }
            catch (UnauthorizedAccessException)
            {
                return [];
            }
            catch (ArgumentException)
            {
                return [];
            }
            catch (NotSupportedException)
            {
                return [];
            }
        }

        private static bool MatchesGlobalJsonPolicy(FXVersion current, GlobalJsonSdkSettings settings)
        {
            if ((!settings.AllowPrerelease && !string.IsNullOrEmpty(current.Pre))
                || settings.RollForward is SdkRollForwardPolicy.Unsupported or SdkRollForwardPolicy.Disable)
            {
                return false;
            }

            FXVersion? requested = settings.RequestedVersion;
            if (requested is null)
            {
                return true;
            }

            int requestedFeatureBand = GetFeatureBand(requested);
            int currentFeatureBand = GetFeatureBand(current);

            if (settings.RollForward is SdkRollForwardPolicy.Patch or SdkRollForwardPolicy.LatestPatch
                && (current.Major != requested.Major
                    || current.Minor != requested.Minor
                    || currentFeatureBand != requestedFeatureBand))
            {
                return false;
            }

            if (settings.RollForward is SdkRollForwardPolicy.Feature or SdkRollForwardPolicy.LatestFeature
                && (current.Major != requested.Major || current.Minor != requested.Minor))
            {
                return false;
            }

            if (settings.RollForward is SdkRollForwardPolicy.Minor or SdkRollForwardPolicy.LatestMinor
                && current.Major != requested.Major)
            {
                return false;
            }

            return FXVersion.Compare(current, requested) >= 0;
        }

        private static bool IsBetterMatch(FXVersion current, FXVersion previous, GlobalJsonSdkSettings settings)
        {
            bool useLatest = settings.RollForward is
                SdkRollForwardPolicy.LatestPatch or
                SdkRollForwardPolicy.LatestFeature or
                SdkRollForwardPolicy.LatestMinor or
                SdkRollForwardPolicy.LatestMajor;

            if (settings.RequestedVersion is null
                || useLatest
                || (current.Major == previous.Major
                    && current.Minor == previous.Minor
                    && GetFeatureBand(current) == GetFeatureBand(previous)))
            {
                return FXVersion.Compare(current, previous) > 0;
            }

            return FXVersion.Compare(current, previous) < 0;
        }

        private static int GetFeatureBand(FXVersion version)
            => version.Patch / 100 * 100;

        private bool TryReadGlobalJsonSdkSettings(
            string globalJsonPath,
            string? requestedVersionString,
            string? dotnetExeDirectory,
            out GlobalJsonSdkSettings? settings)
        {
            settings = null;

            try
            {
                using FileStream stream = File.OpenRead(globalJsonPath);
                using JsonDocument document = JsonDocument.Parse(
                    stream,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip,
                    });

                if (!document.RootElement.TryGetProperty("sdk", out JsonElement sdkElement)
                    || sdkElement.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                FXVersion? requestedVersion = null;
                if (requestedVersionString is not null
                    && (!FXVersion.TryParse(requestedVersionString, out requestedVersion) || requestedVersion is null))
                {
                    return false;
                }

                SdkRollForwardPolicy rollForward = requestedVersion is null
                    ? SdkRollForwardPolicy.LatestMajor
                    : SdkRollForwardPolicy.Patch;

                if (sdkElement.TryGetProperty("rollForward", out JsonElement rollForwardElement)
                    && rollForwardElement.ValueKind != JsonValueKind.Null)
                {
                    if (rollForwardElement.ValueKind != JsonValueKind.String
                        || !TryParseRollForwardPolicy(rollForwardElement.GetString(), out rollForward))
                    {
                        return false;
                    }
                }

                bool allowPrerelease = !_vsSettings.DisallowPrerelease();
                if (sdkElement.TryGetProperty("allowPrerelease", out JsonElement allowPrereleaseElement)
                    && allowPrereleaseElement.ValueKind != JsonValueKind.Null)
                {
                    if (allowPrereleaseElement.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                    {
                        return false;
                    }

                    allowPrerelease = allowPrereleaseElement.GetBoolean();
                }

                if (requestedVersion is not null && !string.IsNullOrEmpty(requestedVersion.Pre))
                {
                    allowPrerelease = true;
                }

                settings = new GlobalJsonSdkSettings(
                    requestedVersion,
                    rollForward,
                    allowPrerelease,
                    GetGlobalJsonSearchPaths(sdkElement, globalJsonPath, dotnetExeDirectory));

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static IReadOnlyList<string?> GetGlobalJsonSearchPaths(
            JsonElement sdkElement,
            string globalJsonPath,
            string? dotnetExeDirectory)
        {
            if (!sdkElement.TryGetProperty("paths", out JsonElement pathsElement)
                || pathsElement.ValueKind == JsonValueKind.Null)
            {
                return [dotnetExeDirectory];
            }

            if (pathsElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            string globalJsonDirectory = Path.GetDirectoryName(globalJsonPath)!;
            var searchPaths = new List<string?>();

            foreach (JsonElement pathElement in pathsElement.EnumerateArray())
            {
                if (pathElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string path = pathElement.GetString()!;
                searchPaths.Add(
                    path == "$host$"
                        ? dotnetExeDirectory
                        : IsPathFullyQualified(path)
                            ? path
                            : Path.Combine(globalJsonDirectory, path));
            }

            return searchPaths;
        }

        private static bool IsPathFullyQualified(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                return false;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return path.Length > 0 && path[0] == Path.DirectorySeparatorChar;
            }

            return path.StartsWith(@"\\", StringComparison.Ordinal)
                || (path.Length >= 3
                    && path[1] == Path.VolumeSeparatorChar
                    && (path[2] == Path.DirectorySeparatorChar || path[2] == Path.AltDirectorySeparatorChar));
        }

        private static bool TryParseRollForwardPolicy(string? value, out SdkRollForwardPolicy policy)
        {
            policy = value?.ToLowerInvariant() switch
            {
                "disable" => SdkRollForwardPolicy.Disable,
                "patch" => SdkRollForwardPolicy.Patch,
                "feature" => SdkRollForwardPolicy.Feature,
                "minor" => SdkRollForwardPolicy.Minor,
                "major" => SdkRollForwardPolicy.Major,
                "latestpatch" => SdkRollForwardPolicy.LatestPatch,
                "latestfeature" => SdkRollForwardPolicy.LatestFeature,
                "latestminor" => SdkRollForwardPolicy.LatestMinor,
                "latestmajor" => SdkRollForwardPolicy.LatestMajor,
                _ => SdkRollForwardPolicy.Unsupported,
            };

            return policy != SdkRollForwardPolicy.Unsupported;
        }

        public Version GetMinimumMSBuildVersion(string netcoreSdkDir)
        {
            return s_minimumMSBuildVersions.GetOrAdd(
                netcoreSdkDir,
                dir =>
                {
                    string minimumVersionFilePath = Path.Combine(netcoreSdkDir, "minimumMSBuildVersion");
                    if (!File.Exists(minimumVersionFilePath))
                    {
                        // smallest version that had resolver support and also
                        // greater than or equal to the version required by any 
                        // .NET Core SDK that did not have this file.
                        return new Version(15, 3, 0);
                    }

                    return Version.Parse(File.ReadLines(minimumVersionFilePath).First().Trim());
                });
        }

        public SdkResolutionResult ResolveNETCoreSdkDirectory(string? globalJsonStartDir, Version msbuildVersion, bool isRunningInVisualStudio, string? dotnetExeDir)
        {
            var result = NETCoreSdkResolverNativeWrapper.ResolveSdk(dotnetExeDir, globalJsonStartDir, _vsSettings.DisallowPrerelease());

            string? mostCompatible = result.ResolvedSdkDirectory;
            if (result.ResolvedSdkDirectory == null
                && result.GlobalJsonPath != null
                && isRunningInVisualStudio)
            {
                result.FailedToResolveSDKSpecifiedInGlobalJson = true;
                // We need the SDK to be version 5 or higher to ensure that we generate a build error when we fail to resolve the SDK specified by global.json
                mostCompatible = GetMostCompatibleSdk(dotnetExeDir, msbuildVersion, 5);
            }
            else if (result.ResolvedSdkDirectory != null
                     && msbuildVersion < GetMinimumMSBuildVersion(result.ResolvedSdkDirectory))
            {
                // hostfxr ignores invalid_json and invalid_data, but __invalid_data_no_fallback still uses the
                // parsed SDK settings (for example, an exact version whose feature band is below 100).
                bool globalJsonSettingsWereUsed = result.GlobalJsonPath is not null
                    && (result.GlobalJsonState is null
                        || string.Equals(result.GlobalJsonState, "valid", StringComparison.Ordinal)
                        || string.Equals(result.GlobalJsonState, "__invalid_data_no_fallback", StringComparison.Ordinal));

                string? compatibleSdk = globalJsonSettingsWereUsed
                    ? GetMostCompatibleSdkForGlobalJson(result, dotnetExeDir, msbuildVersion)
                    : GetMostCompatibleSdk(dotnetExeDir, msbuildVersion);

                // If global.json does not permit an MSBuild-compatible SDK, retain the SDK selected by hostfxr
                // so the caller reports the minimum MSBuild version error for the requested SDK range.
                mostCompatible = compatibleSdk ?? mostCompatible;
            }

            if (mostCompatible != null)
            {
                result.ResolvedSdkDirectory = mostCompatible;
            }

            return result;
        }
    }
}
