
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Semver;
using Serde;
using Serde.Json;
using StaticCs.Collections;

namespace Microsoft.DotNet.DNVM;

[GenerateSerde]
/// <summary>
/// Holds the simple name of a directory that contains one or more SDKs and lives under DNVM_HOME.
/// This is a wrapper to prevent being used directly as a path.
/// </summary>
public sealed partial record SdkDirName(string Name)
{
    public string Name { get; init; } = Name.ToLower();
}

public static partial class ManifestUtils
{
    public static async Task<Manifest> ReadOrCreateManifest(DnvmEnv fs)
    {
        try
        {
            return await fs.ReadManifest();
        }
        // Not found is expected
        catch (Exception e) when (e is DirectoryNotFoundException or FileNotFoundException) { }

        return Manifest.Empty;
    }

    public static EqArray<RegisteredChannel> TrackedChannels(this Manifest manifest)
    {
        return manifest.RegisteredChannels.Where(x => !x.Untracked).ToEq();
    }

    /// <summary>
    /// Calculates the version of the installed muxer. This is
    /// Max(<all installed _runtime_ versions>).
    /// If no SDKs are installed, returns null.
    /// </summary>
    public static SemVersion? MuxerVersion(this Manifest manifest, SdkDirName dir)
    {
        var installedSdks = manifest
            .InstalledSdks
            .Where(s => s.SdkDirName == dir)
            .ToList();
        if (installedSdks.Count == 0)
        {
            return null;
        }
        return installedSdks
            .Select(s => s.RuntimeVersion)
            .Max(SemVersion.SortOrderComparer);
    }

    public static Manifest AddSdk(
        this Manifest manifest,
        SemVersion semVersion,
        Channel? c = null,
        SdkDirName? sdkDirParam = null)
    {
        if (sdkDirParam is not {} sdkDir)
        {
            sdkDir = DnvmEnv.DefaultSdkDirName;
        }
        var installedSdk = new InstalledSdk() {
            SdkDirName = sdkDir,
            SdkVersion = semVersion,
            RuntimeVersion = semVersion,
            AspNetVersion = semVersion,
            ReleaseVersion = semVersion,
        };
        return manifest.AddSdk(installedSdk, c);
    }

    public static Manifest AddSdk(this Manifest manifest,
        InstalledSdk sdk,
        Channel? c = null)
    {
        var installedSdks = manifest.InstalledSdks;
        if (!installedSdks.Contains(sdk))
        {
            installedSdks = installedSdks.Add(sdk);
        }
        EqArray<RegisteredChannel> allChannels = manifest.RegisteredChannels;
        if (allChannels.FirstOrNull(x => !x.Untracked && x.ChannelName == c && x.SdkDirName == sdk.SdkDirName) is { } oldTracked)
        {
            var installedSdkVersions = oldTracked.InstalledSdkVersions;
            var newTracked = installedSdkVersions.Contains(sdk.SdkVersion)
                ? oldTracked
                : oldTracked with {
                    InstalledSdkVersions = installedSdkVersions.Add(sdk.SdkVersion)
                };
            allChannels = allChannels.Replace(oldTracked, newTracked);
        }
        else if (c is not null)
        {
            allChannels = allChannels.Add(new RegisteredChannel {
                ChannelName = c,
                SdkDirName = sdk.SdkDirName,
                InstalledSdkVersions = [ sdk.SdkVersion ]
            });
        }
        return manifest with {
            InstalledSdks = installedSdks,
            RegisteredChannels = allChannels,
        };
    }

    public static bool IsSdkInstalled(Manifest manifest, SemVersion version, SdkDirName dirName)
    {
        return manifest.InstalledSdks.Any(s => s.SdkVersion == version && s.SdkDirName == dirName);
    }

    /// <summary>
    /// Either reads a manifest in the current format, or reads a
    /// manifest in the old format and converts it to the new format.
    /// </summary>
    public static async Task<Manifest> DeserializeNewOrOldManifest(
        ScopedHttpClient httpClient,
        string manifestSrc,
        IEnumerable<string> releasesUrls)
    {
        var version = JsonSerializer.Deserialize<ManifestVersionOnly>(manifestSrc).Version;
        // Handle versions that don't need the release index to convert
        Manifest? manifest = version switch {
            ManifestV5.VersionField => JsonSerializer.Deserialize<ManifestV5>(manifestSrc).Convert().Convert().Convert(),
            ManifestV6.VersionField => JsonSerializer.Deserialize<ManifestV6>(manifestSrc).Convert().Convert(),
            ManifestV7.VersionField => JsonSerializer.Deserialize<ManifestV7>(manifestSrc).Convert(),
            Manifest.VersionField => JsonSerializer.Deserialize<Manifest>(manifestSrc),
            _ => null
        };
        if (manifest is not null)
        {
            return manifest;
        }

        // Retrieve release index and convert
        var releasesIndex = await DotnetReleasesIndex.FetchLatestIndex(httpClient, releasesUrls);
        return version switch
        {
            // The first version didn't have a version field
            null => (await JsonSerializer.Deserialize<ManifestV1>(manifestSrc)
                .Convert().Convert().Convert().Convert(httpClient, releasesIndex)).Convert().Convert().Convert(),
            ManifestV2.VersionField => (await JsonSerializer.Deserialize<ManifestV2>(manifestSrc)
                .Convert().Convert().Convert(httpClient, releasesIndex)).Convert().Convert().Convert(),
            ManifestV3.VersionField => (await JsonSerializer.Deserialize<ManifestV3>(manifestSrc)
                .Convert().Convert(httpClient, releasesIndex)).Convert().Convert().Convert(),
            ManifestV4.VersionField => (await JsonSerializer.Deserialize<ManifestV4>(manifestSrc)
                .Convert(httpClient, releasesIndex)).Convert().Convert().Convert(),
            _ => throw new InvalidDataException("Unknown manifest version: " + version)
        };
    }

    [GenerateDeserialize]
    private sealed partial class ManifestVersionOnly
    {
        public int? Version { get; init; }
    }
}