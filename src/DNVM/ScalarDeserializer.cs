
using System;
using System.Buffers;
using Serde;

namespace Dnvm;

public sealed class ScalarDeserializer(string s) : IDeserializer
{
    public bool ReadBool()
        => bool.Parse(s);

    public byte ReadU8()
        => byte.Parse(s);

    public char ReadChar()
        => char.Parse(s);

    public decimal ReadDecimal()
        => decimal.Parse(s);

    public double ReadF64() => double.Parse(s);

    public float ReadF32() => float.Parse(s);

    public short ReadI16() => short.Parse(s);

    public int ReadI32() => int.Parse(s);

    public long ReadI64() => long.Parse(s);

    public sbyte ReadI8() => sbyte.Parse(s);

    public string ReadString() => s;

    public ushort ReadU16() => ushort.Parse(s);

    public uint ReadU32() => uint.Parse(s);

    public ulong ReadU64() => ulong.Parse(s);

    public DateTime ReadDateTime()
        => DateTime.Parse(s);
    public void ReadBytes(IBufferWriter<byte> writer)
        => throw new DeserializeException("Found bytes, expected scalar");

    void IDisposable.Dispose() { }

    T IDeserializer.ReadNullableRef<T>(IDeserialize<T> deserialize)
    {
        return deserialize.Deserialize(this);
    }

    ITypeDeserializer IDeserializer.ReadType(ISerdeInfo typeInfo)
        => throw new DeserializeException("Found nullable ref, expected scalar");
}