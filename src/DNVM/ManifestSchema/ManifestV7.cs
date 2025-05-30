
using System;
using System.Linq;
using Semver;
using Serde;
using StaticCs.Collections;

namespace Dnvm;

[GenerateSerde]
public sealed partial record ManifestV7
{
    public static readonly ManifestV7 Empty = new();

    // Serde doesn't serialize consts, so we have a separate property below for serialization.
    public const int VersionField = 7;

    [SerdeMemberOptions(SkipDeserialize = true)]
    public int Version => VersionField;

    public SdkDirName CurrentSdkDir { get; init; } = DnvmEnv.DefaultSdkDirName;
    public EqArray<InstalledSdkV7> InstalledSdks { get; init; } = [];
    public EqArray<RegisteredChannelV7> RegisteredChannels { get; init; } = [];

    internal ManifestV7 TrackChannel(RegisteredChannelV7 channel)
    {
        var existing = RegisteredChannels.FirstOrNull(c =>
            c.ChannelName == channel.ChannelName && c.SdkDirName == channel.SdkDirName);
        if (existing is null)
        {
            return this with
            {
                RegisteredChannels = RegisteredChannels.Add(channel)
            };
        }
        else if (existing is { Untracked: true })
        {
            var newVersions = existing.InstalledSdkVersions.Concat(channel.InstalledSdkVersions).Distinct().ToEq();
            return this with
            {
                RegisteredChannels = RegisteredChannels.Replace(existing, existing with
                {
                    InstalledSdkVersions = newVersions,
                    Untracked = false,
                })
            };
        }
        throw new InvalidOperationException("Channel already tracked");
    }

    internal ManifestV7 UntrackChannel(Channel channel)
    {
        return this with
        {
            RegisteredChannels = RegisteredChannels.Select(c =>
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
public partial record RegisteredChannelV7
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
public partial record InstalledSdkV7
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

public static partial class ManifestV7Convert
{
    public static ManifestV7 Convert(this ManifestV6 v6) => new ManifestV7
    {
        InstalledSdks = v6.InstalledSdks.SelectAsArray(v => v.Convert()).ToEq(),
        RegisteredChannels = v6.TrackedChannels.SelectAsArray(c => c.Convert()).ToEq(),
    };

    public static InstalledSdkV7 Convert(this InstalledSdkV6 v6) => new InstalledSdkV7 {
        ReleaseVersion = v6.ReleaseVersion,
        SdkVersion = v6.SdkVersion,
        RuntimeVersion = v6.RuntimeVersion,
        AspNetVersion = v6.AspNetVersion,
        SdkDirName = v6.SdkDirName,
    };

    public static RegisteredChannelV7 Convert(this TrackedChannelV6 v6) => new RegisteredChannelV7 {
        ChannelName = v6.ChannelName,
        SdkDirName = v6.SdkDirName,
        InstalledSdkVersions = v6.InstalledSdkVersions,
        Untracked = v6.Untracked,
    };
}