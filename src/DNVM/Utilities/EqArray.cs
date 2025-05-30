
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Serde;

namespace Dnvm;

public static class EqArray
{
    public static EqArray<T> ToEq<T>(this IEnumerable<T> array) => new(array.ToImmutableArray());
    public static EqArray<T> ToEq<T>(this ImmutableArray<T> array) => new(array);
    public static EqArray<T> Create<T>(params T[] array) => ImmutableArray.Create(array).ToEq();
    public static EqArray<T> Create<T>(ReadOnlySpan<T> span) => ImmutableArray.Create(span).ToEq();
}

[SerdeTypeOptions(Proxy = typeof(EqArrayProxy))]
[CollectionBuilder(typeof(EqArray), nameof(EqArray.Create))]
public readonly struct EqArray<T>(ImmutableArray<T> value) : IReadOnlyCollection<T>, IEquatable<EqArray<T>>
{
    public ImmutableArray<T> Array => value;

    public static readonly EqArray<T> Empty = ImmutableArray<T>.Empty.ToEq();

    public int Length => value.Length;

    int IReadOnlyCollection<T>.Count => value.Length;

    public override bool Equals(object? obj) => obj is EqArray<T> other && Equals(other);

    public bool Equals(EqArray<T> other) => value.SequenceEqual(other.Array);

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)value).GetEnumerator();

    public override int GetHashCode()
    {
        return value.Aggregate(0, (acc, item) => HashCode.Combine(acc, item));
    }

    public override string ToString()
    {
        return "[ " + string.Join(", ", value) + " ]";
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)value).GetEnumerator();
    }

    public T this[int index] => value[index];

    public EqArray<T> Add(T item) => new(value.Add(item));

    public EqArray<T> Replace(T oldItem, T newItem) => new(value.Replace(oldItem, newItem));
}

public static class EqArrayProxy
{
    private static readonly ISerdeInfo s_typeInfo = Serde.SerdeInfo.MakeEnumerable(nameof(EqArray));
    public sealed class Ser<T, TProvider> : ISerializeProvider<EqArray<T>>, ISerialize<EqArray<T>>
        where TProvider : ISerializeProvider<T>
    {
        public static readonly Ser<T, TProvider> Instance = new();
        static ISerialize<EqArray<T>> ISerializeProvider<EqArray<T>>.Instance => Instance;

        public ISerdeInfo SerdeInfo => s_typeInfo;

        void ISerialize<EqArray<T>>.Serialize(EqArray<T> value, ISerializer serializer)
        {
            ImmutableArrayProxy.Ser<T, TProvider>.Instance.Serialize(value.Array, serializer);
        }
    }

    public sealed class De<T, TProvider> : IDeserializeProvider<EqArray<T>>, IDeserialize<EqArray<T>>
        where TProvider : IDeserializeProvider<T>
    {
        public static readonly De<T, TProvider> Instance = new();
        static IDeserialize<EqArray<T>> IDeserializeProvider<EqArray<T>>.Instance => Instance;

        public ISerdeInfo SerdeInfo => s_typeInfo;
        EqArray<T> IDeserialize<EqArray<T>>.Deserialize(IDeserializer deserializer)
        {
            return ImmutableArrayProxy.De<T, TProvider>.Instance.Deserialize(deserializer).ToEq();
        }
    }
}