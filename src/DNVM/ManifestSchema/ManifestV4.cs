
using System;
using System.Collections.Immutable;
using System.Linq;
using Serde;
using StaticCs.Collections;

namespace Dnvm;

[GenerateSerde]
public sealed partial record ManifestV4
{
    public static readonly ManifestV4 Empty = new();

    // Serde doesn't serialize consts, so we have a separate property below for serialization.
    public const int VersionField = 4;

    [SerdeMemberOptions(SkipDeserialize = true)]
    public int Version => VersionField;

    public SdkDirName CurrentSdkDir { get; init; } = DnvmEnv.DefaultSdkDirName;
    public ImmutableArray<InstalledSdkV4> InstalledSdkVersions { get; init; } = ImmutableArray<InstalledSdkV4>.Empty;
    public ImmutableArray<TrackedChannelV4> TrackedChannels { get; init; } = ImmutableArray<TrackedChannelV4>.Empty;

    public override string ToString()
    {
        return $"ManifestV4 {{ Version = {Version}, "
            + $"InstalledSdkV4Version = [{InstalledSdkVersions.SeqToString()}, "
            + $"TrackedChannelV4s = [{TrackedChannels.SeqToString()}] }}";
    }

    public bool Equals(ManifestV4? other)
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

    internal ManifestV4 Untrack(Channel channel)
    {
        return this with
        {
            TrackedChannels = TrackedChannels.Where(c => c.ChannelName != channel).ToImmutableArray()
        };
    }
}

[GenerateSerde]
public sealed partial record TrackedChannelV4
{
    public required Channel ChannelName { get; init; }
    public required SdkDirName SdkDirName { get; init; }
    public ImmutableArray<string> InstalledSdkVersions { get; init; } = ImmutableArray<string>.Empty;

    public bool Equals(TrackedChannelV4? other)
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
public sealed partial record InstalledSdkV4
{
    public required string Version { get; init; }
    public SdkDirName SdkDirName { get; init; } = DnvmEnv.DefaultSdkDirName;
}

public static partial class ManifestV4Convert
{
    public static ManifestV4 Convert(this ManifestV3 v3)
    {
        return new ManifestV4
        {
            InstalledSdkVersions = v3.InstalledSdkVersions.SelectAsArray(v => v.Convert()),
            TrackedChannels = v3.TrackedChannels.SelectAsArray(c => c.Convert()),
        };
    }

    public static InstalledSdkV4 Convert(this InstalledSdkV3 v3)
    {
        return new InstalledSdkV4 {
            SdkDirName = v3.SdkDirName,
            Version = v3.Version,
        };
    }

    public static TrackedChannelV4 Convert(this TrackedChannelV3 v3) => new TrackedChannelV4 {
        ChannelName = v3.ChannelName,
        SdkDirName = v3.SdkDirName,
        InstalledSdkVersions = v3.InstalledSdkVersions,
    };
}

public static partial class ManifestV4Utils
{
    public static ManifestV4 AddSdk(this ManifestV4 manifest, InstalledSdkV4 sdk, Channel c)
    {
        ManifestV4 newManifest;
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
                TrackedChannels = manifest.TrackedChannels.Add(new TrackedChannelV4()
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