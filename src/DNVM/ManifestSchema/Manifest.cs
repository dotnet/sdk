
using System;
using System.Linq;
using Semver;
using Serde;
using StaticCs.Collections;

namespace Dnvm;

[GenerateSerde]
public sealed partial record Manifest
{
    public static readonly Manifest Empty = new();

    // Serde doesn't serialize consts, so we have a separate property below for serialization.
    public const int VersionField = 8;

    [SerdeMemberOptions(SkipDeserialize = true)]
    public int Version => VersionField;

    public bool PreviewsEnabled { get; init; } = false;
    public SdkDirName CurrentSdkDir { get; init; } = DnvmEnv.DefaultSdkDirName;
    public EqArray<InstalledSdk> InstalledSdks { get; init; } = [];
    public EqArray<RegisteredChannel> RegisteredChannels { get; init; } = [];

    public Manifest TrackChannel(RegisteredChannel channel)
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

    internal Manifest UntrackChannel(Channel channel)
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
public partial record RegisteredChannel
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
public partial record InstalledSdk
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

public static partial class ManifestConvert
{
    public static Manifest Convert(this ManifestV7 v7) => new Manifest
    {
        InstalledSdks = v7.InstalledSdks.SelectAsArray(v => v.Convert()).ToEq(),
        RegisteredChannels = v7.RegisteredChannels.SelectAsArray(c => c.Convert()).ToEq(),
    };

    public static InstalledSdk Convert(this InstalledSdkV7 v7) => new InstalledSdk {
        ReleaseVersion = v7.ReleaseVersion,
        SdkVersion = v7.SdkVersion,
        RuntimeVersion = v7.RuntimeVersion,
        AspNetVersion = v7.AspNetVersion,
        SdkDirName = v7.SdkDirName,
    };

    public static RegisteredChannel Convert(this RegisteredChannelV7 v7) => new RegisteredChannel {
        ChannelName = v7.ChannelName,
        SdkDirName = v7.SdkDirName,
        InstalledSdkVersions = v7.InstalledSdkVersions,
        Untracked = v7.Untracked,
    };
}