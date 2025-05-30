
using Serde;

namespace Dnvm;

/// <summary>
/// Serializes the <see cref="SdkDirName.Name"/> directly as a string.
/// </summary>
internal sealed class SdkDirNameProxy : ISerde<SdkDirName>, ISerdeProvider<SdkDirNameProxy, SdkDirNameProxy, SdkDirName>
{
    public static SdkDirNameProxy Instance { get; } = new();
    public ISerdeInfo SerdeInfo => StringProxy.SerdeInfo;

    public SdkDirName Deserialize(IDeserializer deserializer)
        => new SdkDirName(StringProxy.Instance.Deserialize(deserializer));

    public void Serialize(SdkDirName value, ISerializer serializer)
    {
        serializer.WriteString(value.Name);
    }
}