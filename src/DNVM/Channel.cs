
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Semver;

namespace Microsoft.DotNet.DNVM;

public static class Channels
{
    public static string GetDesc(this Channel c) => c switch
    {
        Channel.VersionedMajorMinor v => $"The latest version in the {v} support channel",
        Channel.VersionedFeature v => $"The latest version in the {v} support channel",
        Channel.Lts => "The latest version in Long-Term support",
        Channel.Sts => "The latest version in Short-Term support",
        Channel.Latest => "The latest supported version from either the LTS or STS support channels.",
        Channel.Preview => "The latest preview version",
    };
}


public abstract partial record Channel
{
    private Channel() { }

    /// <summary>
    /// A major-minor versioned channel.
    /// </summary>
    public sealed partial record VersionedMajorMinor(int Major, int Minor) : Channel;
    /// <summary>
    /// A major-minor-patch versioned channel.
    /// </summary>
    /// <param name="FeatureLevel"> The feature level of the version, e.g. 1 in 9.0.100</param>
    public sealed partial record VersionedFeature(int Major, int Minor, int FeatureLevel) : Channel;
    /// <summary>
    /// Newest Long Term Support release.
    /// </summary>
    public sealed partial record Lts : Channel;
    /// <summary>
    /// Newest Short Term Support release.
    /// </summary>
    public sealed partial record Sts : Channel;
    /// <summary>
    /// Latest supported version from either the LTS or STS support channels.
    /// </summary>
    public sealed partial record Latest : Channel;
    /// <summary>
    /// Latest preview version.
    /// </summary>
    public sealed partial record Preview : Channel;
}

partial record Channel : ISerializeProvider<Channel>
{
    public abstract string GetDisplayName();
    public sealed override string ToString() => GetDisplayName();
    public string GetLowerName() => GetDisplayName().ToLowerInvariant();

    static ISerialize<Channel> ISerializeProvider<Channel>.Instance => Serialize.Instance;

    private sealed class Serialize : ISerialize<Channel>
    {
        public static readonly Serialize Instance = new();
        private Serialize() { }

        /// <summary>
        /// Serialize as a string.
        /// </summary>
        void ISerialize<Channel>.Serialize(Channel channel, ISerializer serializer)
            => serializer.WriteString(channel.GetLowerName());
    }

    partial record VersionedMajorMinor
    {
        public override string GetDisplayName() => $"{Major}.{Minor}";
    }
    partial record VersionedFeature
    {
        public override string GetDisplayName() => $"{Major}.{Minor}.{FeatureLevel}xx";
    }
    partial record Lts : Channel
    {
        public override string GetDisplayName() => "LTS";
    }
    partial record Sts : Channel
    {
        public override string GetDisplayName() => "STS";
    }
    partial record Latest : Channel
    {
        public override string GetDisplayName() => "Latest";
    }
    partial record Preview : Channel
    {
        public override string GetDisplayName() => "Preview";
    }
}

partial record Channel : IDeserializeProvider<Channel>
{
    static IDeserialize<Channel> IDeserializeProvider<Channel>.Instance => DeserializeProxy.Instance;

    public static Channel FromString(string str)
    {
        switch (str)
        {
            case "lts": return new Lts();
            case "sts": return new Sts();
            case "latest": return new Latest();
            case "preview": return new Preview();
            default:
                var components = str.Split('.');
                switch (components)
                {
                    case [_, _]:
                        var major = int.Parse(components[0]);
                        var minor = int.Parse(components[1]);
                        return new VersionedMajorMinor(major, minor);
                    case [_, _, _]:
                        if (components[2] is not [<= '9' and >= '0', 'x', 'x'])
                        {
                            throw new DeserializeException($"Feature band must be 3 characters and end in 'xx': {str}");
                        }
                        major = int.Parse(components[0]);
                        minor = int.Parse(components[1]);
                        var featureLevel = components[2][0] - '0';
                        return new VersionedFeature(major, minor, featureLevel);
                    default:
                        throw new DeserializeException($"Invalid channel version: {str}");
                }
        }
    }

    private sealed class DeserializeProxy : IDeserialize<Channel>
    {
        public static readonly DeserializeProxy Instance = new();

        /// <summary>
        /// Deserialize as a string.
        /// </summary>
        public Channel Deserialize(IDeserializer deserializer)
            => FromString(StringProxy.Instance.Deserialize(deserializer));
    }
}
