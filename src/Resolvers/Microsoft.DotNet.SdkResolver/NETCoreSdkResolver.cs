// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
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

        private string? GetMostCompatibleSdkInRange(
            string? dotnetExeDirectory,
            Version msbuildVersion,
            string requestedVersionString,
            string resolvedSdkDirectory)
        {
            string resolvedVersionString = new DirectoryInfo(resolvedSdkDirectory).Name;
            if (!FXVersion.TryParse(requestedVersionString, out FXVersion? requestedVersion)
                || requestedVersion is null
                || !FXVersion.TryParse(resolvedVersionString, out FXVersion? resolvedVersion)
                || resolvedVersion is null
                || FXVersion.Compare(requestedVersion, resolvedVersion) >= 0
                || !IsSdkUnderDotnetRoot(resolvedSdkDirectory, dotnetExeDirectory))
            {
                return null;
            }

            bool allowPrerelease = !string.IsNullOrEmpty(resolvedVersion.Pre);
            string? bestMatch = null;
            FXVersion? bestVersion = null;

            foreach (string sdkDirectory in GetSdkDirectories(dotnetExeDirectory!))
            {
                string sdkVersionString = new DirectoryInfo(sdkDirectory).Name;
                if (!FXVersion.TryParse(sdkVersionString, out FXVersion? sdkVersion)
                    || sdkVersion is null
                    || FXVersion.Compare(sdkVersion, resolvedVersion) >= 0
                    || FXVersion.Compare(sdkVersion, requestedVersion) < 0
                    || (!allowPrerelease && !string.IsNullOrEmpty(sdkVersion.Pre))
                    || msbuildVersion < GetMinimumMSBuildVersion(sdkDirectory))
                {
                    continue;
                }

                if (bestVersion is null || FXVersion.Compare(sdkVersion, bestVersion) > 0)
                {
                    bestMatch = sdkDirectory;
                    bestVersion = sdkVersion;
                }
            }

            return bestMatch;
        }

        private static IEnumerable<string> GetSdkDirectories(string dotnetExeDirectory)
        {
            string sdkRoot = Path.Combine(dotnetExeDirectory, "sdk");
            if (!Directory.Exists(sdkRoot))
            {
                return [];
            }

            try
            {
                return Directory.GetDirectories(sdkRoot)
                    .Where(sdk => File.Exists(Path.Combine(sdk, "dotnet.dll")));
            }
            catch (IOException)
            {
                return [];
            }
            catch (UnauthorizedAccessException)
            {
                return [];
            }
        }

        private static bool IsSdkUnderDotnetRoot(string sdkDirectory, string? dotnetExeDirectory)
        {
            if (dotnetExeDirectory is null)
            {
                return false;
            }

            string? sdkRoot = Path.GetDirectoryName(sdkDirectory);
            if (sdkRoot is null)
            {
                return false;
            }

            StringComparison comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            return string.Equals(
                Path.GetFullPath(sdkRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(Path.Combine(dotnetExeDirectory, "sdk")).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                comparison);
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
                string? compatibleSdk = result.GlobalJsonPath is null
                    ? GetMostCompatibleSdk(dotnetExeDir, msbuildVersion)
                    : result.RequestedVersion is null
                        ? null
                        : GetMostCompatibleSdkInRange(
                            dotnetExeDir,
                            msbuildVersion,
                            result.RequestedVersion,
                            result.ResolvedSdkDirectory);

                // A global.json fallback is limited to versions between the requested and resolved SDKs.
                // If no compatible SDK exists in that range, retain the hostfxr selection so the caller
                // reports its minimum MSBuild version requirement.
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
