
using System;
using System.Collections.Immutable;
using System.Linq;
using Serde;

namespace Dnvm;

[GenerateSerde]
internal sealed partial record ManifestV2
{
    // Serde doesn't serialize consts, so we have a separate property below for serialization.
    public const int VersionField = 2;

    [SerdeMemberOptions(SkipDeserialize = true)]
    public int Version => VersionField;
    public required ImmutableArray<string> InstalledSdkVersions { get; init; }
    public required ImmutableArray<TrackedChannelV2> TrackedChannels { get; init; }

    public override string ToString()
    {
        return $"Manifest {{ Version = {Version}, "
            + $"InstalledSdkVersion = [{InstalledSdkVersions.SeqToString()}, "
            + $"TrackedChannels = [{TrackedChannels.SeqToString()}] }}";
    }

    public bool Equals(ManifestV2? other)
    {
        return other is not null && this.InstalledSdkVersions.SequenceEqual(other.InstalledSdkVersions) &&
            this.TrackedChannels.SequenceEqual(other.TrackedChannels);
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
internal sealed partial record TrackedChannelV2
{
    public required Channel ChannelName { get; init; }
    public ImmutableArray<string> InstalledSdkVersions { get; init; }

    public bool Equals(TrackedChannelV2? other)
    {
        return other is not null &&
            this.ChannelName == other.ChannelName &&
            this.InstalledSdkVersions.SequenceEqual(other.InstalledSdkVersions);
    }

    public override int GetHashCode()
    {
        int code = ChannelName.GetHashCode();
        foreach (var item in InstalledSdkVersions)
        {
            code = HashCode.Combine(code, item);
        }
        return code;
    }
}

static partial class ManifestV2Convert
{
    public static ManifestV2 Convert(this ManifestV1 v1)
    {
        return new ManifestV2 {
            InstalledSdkVersions = v1.Workloads.Select(w => w.Version).ToImmutableArray(),
            TrackedChannels = ImmutableArray<TrackedChannelV2>.Empty
        };
    }
}