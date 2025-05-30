
using System;
using Semver;
using Serde;

namespace Dnvm;

/// <summary>
/// Serializes as a string.
/// </summary>
internal sealed class SemVersionProxy : ISerde<SemVersion>, ISerdeProvider<SemVersionProxy, SemVersionProxy, SemVersion>
{
    static SemVersionProxy ISerdeProvider<SemVersionProxy, SemVersionProxy, SemVersion>.Instance { get; } = new SemVersionProxy();
    private SemVersionProxy() { }

    public ISerdeInfo SerdeInfo { get; } = StringProxy.SerdeInfo;

    public SemVersion Deserialize(IDeserializer deserializer)
    {
        var str = deserializer.ReadString();
        if (SemVersion.TryParse(str, SemVersionStyles.Strict, out var version))
        {
            return version;
        }
        throw new DeserializeException($"Version string '{str}' is not a valid SemVersion.");
    }

    public void Serialize(SemVersion value, ISerializer serializer)
    {
        serializer.WriteString(value.ToString());
    }
}