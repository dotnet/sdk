
using System;
using System.Collections.Immutable;
using System.Linq;
using Serde;
using StaticCs.Collections;

namespace Dnvm;

[GenerateSerde]
public sealed partial record ManifestV3
{
    public static readonly ManifestV3 Empty = new();

    // Serde doesn't serialize consts, so we have a separate property below for serialization.
    public const int VersionField = 3;

    [SerdeMemberOptions(SkipDeserialize = true)]
    public static int Version => VersionField;

    public ImmutableArray<InstalledSdkV3> InstalledSdkVersions { get; init; } = ImmutableArray<InstalledSdkV3>.Empty;
    public ImmutableArray<TrackedChannelV3> TrackedChannels { get; init; } = ImmutableArray<TrackedChannelV3>.Empty;

    public override string ToString()
    {
        return $"Manifest {{ Version = {Version}, "
            + $"InstalledSdkVersion = [{InstalledSdkVersions.SeqToString()}, "
            + $"TrackedChannels = [{TrackedChannels.SeqToString()}] }}";
    }

    public bool Equals(ManifestV3? other)
    {
        return other is not null && InstalledSdkVersions.SequenceEqual(other.InstalledSdkVersions) &&
            TrackedChannels.SequenceEqual(other.TrackedChannels);
    }

    public override int GetHashCode()
    {
        int code = 0;
        foreach (var item in InstalledSdkVersions)
        {
            code = HashCode.Combine(code, item);
        }
        foreach (var item in TrackedChannels)
        {
            code = HashCode.Combine(code, item);
        }
        return code;
    }
}

[GenerateSerde]
public sealed partial record TrackedChannelV3
{
    public required Channel ChannelName { get; init; }
    public required SdkDirName SdkDirName { get; init; }
    public ImmutableArray<string> InstalledSdkVersions { get; init; } = ImmutableArray<string>.Empty;

    public bool Equals(TrackedChannelV3? other)
    {
        return other is not null &&
            ChannelName == other.ChannelName &&
            SdkDirName == other.SdkDirName &&
            InstalledSdkVersions.SequenceEqual(other.InstalledSdkVersions);
    }

    public override int GetHashCode()
    {
        int code = 0;
        code = HashCode.Combine(code, ChannelName);
        code = HashCode.Combine(code, SdkDirName);
        foreach (string item in InstalledSdkVersions)
        {
            code = HashCode.Combine(code, item);
        }
        return code;
    }
}

[GenerateSerde]
public sealed partial record InstalledSdkV3
{
    public required string Version { get; init; }
    public SdkDirName SdkDirName { get; init; } = DnvmEnv.DefaultSdkDirName;
}

static class ManifestConvertV3
{
    internal static ManifestV3 Convert(this ManifestV2 v2)
    {
        return new ManifestV3 {
            InstalledSdkVersions = v2.InstalledSdkVersions.Select(v => new InstalledSdkV3 {
                Version = v,
                // Before V3, all SDKs were installed to the default dir
                SdkDirName = DnvmEnv.DefaultSdkDirName
            }).ToImmutableArray(),
            TrackedChannels = v2.TrackedChannels.Select(c => new TrackedChannelV3 {
                ChannelName = c.ChannelName,
                SdkDirName = DnvmEnv.DefaultSdkDirName,
                InstalledSdkVersions = c.InstalledSdkVersions
            }).ToImmutableArray(),
        };
    }
}

public static partial class ManifestV3Utils
{
    public static ManifestV3 AddSdk(this ManifestV3 manifest, InstalledSdkV3 sdk, Channel c)
    {
        ManifestV3 newManifest;
        if (manifest.TrackedChannels.FirstOrNull(x => x.ChannelName == c) is { } trackedChannel)
        {
            if (trackedChannel.InstalledSdkVersions.Contains(sdk.Version))
            {
                return manifest;
            }
            newManifest = manifest with
            {
                TrackedChannels = manifest.TrackedChannels.Select(x => x.ChannelName == c
                    ? x with { InstalledSdkVersions = x.InstalledSdkVersions.Add(sdk.Version) }
                    : x).ToImmutableArray(),
                InstalledSdkVersions = manifest.InstalledSdkVersions.Add(sdk)
            };
        }
        else
        {
            newManifest = manifest with
            {
                TrackedChannels = manifest.TrackedChannels.Add(new TrackedChannelV3()
                {
                    ChannelName = c,
                    SdkDirName = sdk.SdkDirName,
                    InstalledSdkVersions = ImmutableArray.Create(sdk.Version)
                }),
                InstalledSdkVersions = manifest.InstalledSdkVersions.Add(sdk)
            };
        }
        return newManifest;
    }
}