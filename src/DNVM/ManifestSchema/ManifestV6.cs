using System.Linq;
using Semver;
using Serde;

namespace Dnvm;

[GenerateSerde]
public sealed partial record ManifestV6
{
    public static readonly ManifestV6 Empty = new();

    // Serde doesn't serialize consts, so we have a separate property below for serialization.
    public const int VersionField = 6;

    [SerdeMemberOptions(SkipDeserialize = true)]
    public int Version => VersionField;

    public SdkDirName CurrentSdkDir { get; init; } = DnvmEnv.DefaultSdkDirName;
    public EqArray<InstalledSdkV6> InstalledSdks { get; init; } = [];
    public EqArray<TrackedChannelV6> TrackedChannels { get; init; } = [];

    internal ManifestV6 Untrack(Channel channel)
    {
        return this with
        {
            TrackedChannels = TrackedChannels.Select(c =>
            {
                if (c.ChannelName == channel)
                {
                    return c with { Untracked = true };
                }
                return c;
            }).ToEq()
        };
    }
}

[GenerateSerde]
public partial record TrackedChannelV6
{
    public required Channel ChannelName { get; init; }
    public required SdkDirName SdkDirName { get; init; }
    [SerdeMemberOptions(
        SerializeProxy = typeof(EqArrayProxy.Ser<SemVersion, SemVersionProxy>),
        DeserializeProxy = typeof(EqArrayProxy.De<SemVersion, SemVersionProxy>))]
    public EqArray<SemVersion> InstalledSdkVersions { get; init; } = EqArray<SemVersion>.Empty;
    public bool Untracked { get; init; } = false;
}

[GenerateSerde]
public partial record InstalledSdkV6
{
    [SerdeMemberOptions(Proxy = typeof(SemVersionProxy))]
    public required SemVersion ReleaseVersion { get; init; }
    [SerdeMemberOptions(Proxy = typeof(SemVersionProxy))]
    public required SemVersion SdkVersion { get; init; }
    [SerdeMemberOptions(Proxy = typeof(SemVersionProxy))]
    public required SemVersion RuntimeVersion { get; init; }
    [SerdeMemberOptions(Proxy = typeof(SemVersionProxy))]
    public required SemVersion AspNetVersion { get; init; }

    public SdkDirName SdkDirName { get; init; } = DnvmEnv.DefaultSdkDirName;
}

public static partial class ManifestV6Convert
{
    public static ManifestV6 Convert(this ManifestV5 v5) => new ManifestV6
    {
        InstalledSdks = v5.InstalledSdkVersions.SelectAsArray(v => v.Convert()).ToEq(),
        TrackedChannels = v5.TrackedChannels.SelectAsArray(c => c.Convert()).ToEq(),
    };

    public static InstalledSdkV6 Convert(this InstalledSdkV5 v5) => new InstalledSdkV6 {
        ReleaseVersion = v5.ReleaseVersion,
        SdkVersion = v5.SdkVersion,
        RuntimeVersion = v5.RuntimeVersion,
        AspNetVersion = v5.AspNetVersion,
        SdkDirName = v5.SdkDirName,
    };

    public static TrackedChannelV6 Convert(this TrackedChannelV5 v5) => new TrackedChannelV6 {
        ChannelName = v5.ChannelName,
        SdkDirName = v5.SdkDirName,
        InstalledSdkVersions = v5.InstalledSdkVersions
    };
}