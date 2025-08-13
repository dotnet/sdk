// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Buffers;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public struct StaticWebAssetEndpointProperty : IComparable<StaticWebAssetEndpointProperty>, IEquatable<StaticWebAssetEndpointProperty>
{
    private static readonly JsonTypeInfo<StaticWebAssetEndpointProperty[]> _jsonTypeInfo =
        StaticWebAssetsJsonSerializerContext.Default.StaticWebAssetEndpointPropertyArray;

    private static readonly Encoding _encoder = Encoding.UTF8;

    // Pre-encoded property names for high-performance serialization
    private static readonly JsonEncodedText NamePropertyName = JsonEncodedText.Encode("Name");
    private static readonly JsonEncodedText ValuePropertyName = JsonEncodedText.Encode("Value");

    public string Name { get; set; }

    public string Value { get; set; }

    public static StaticWebAssetEndpointProperty[] FromMetadataValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        var result = JsonSerializer.Deserialize(value, _jsonTypeInfo);
        Array.Sort(result);
        return result;
    }

    public static void PopulateFromMetadataValue(string value, List<StaticWebAssetEndpointProperty> properties)
    {
        properties.Clear();

        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        // Use stack allocation for small buffers, ArrayPool for larger ones to avoid heap allocation
        var maxByteCount = _encoder.GetMaxByteCount(value.Length);
        byte[] rentedBuffer = null;

#if NET6_0_OR_GREATER
        const int StackAllocThreshold = 1024;
        Span<byte> bytes = maxByteCount <= StackAllocThreshold
            ? stackalloc byte[maxByteCount]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount)).AsSpan(0, maxByteCount);
        var actualByteCount = _encoder.GetBytes(value, bytes);
        var reader = new Utf8JsonReader(bytes.Slice(0, actualByteCount));
#else
        // For .NET Framework and older versions, we always rent from the pool
        byte[] bytes = rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
        var actualByteCount = _encoder.GetBytes(value, 0, value.Length, bytes, 0);
        var reader = new Utf8JsonReader(bytes.AsSpan(0, actualByteCount));
#endif

        try
        {
            PopulateFromMetadataValue(ref reader, properties);
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }

    public static void PopulateFromMetadataValue(ref Utf8JsonReader reader, List<StaticWebAssetEndpointProperty> properties)
    {
        // Expect to be positioned at start of array
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Read(); // Move to start array if not already there
        }
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var property = new StaticWebAssetEndpointProperty();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.ValueTextEquals("Name"u8))
                {
                    reader.Read();
                    // Try to get interned property name first using span comparison
                    var internedName = WellKnownEndpointPropertyNames.TryGetInternedPropertyName(reader.ValueSpan);
                    property.Name = internedName ?? reader.GetString();
                }
                else if (reader.ValueTextEquals("Value"u8))
                {
                    reader.Read();
                    property.Value = reader.GetString();
                }
                else
                {
                    reader.Skip();
                }
            }

            properties.Add(property);
        }
    }

    public static string ToMetadataValue(StaticWebAssetEndpointProperty[] responseHeaders)
    {
        var properties = responseHeaders ?? [];
        Array.Sort(properties);
        return JsonSerializer.Serialize(properties, _jsonTypeInfo);
    }

    internal static string ToMetadataValue(
        List<StaticWebAssetEndpointProperty> properties,
        JsonWriterContext context)
    {
        if (properties == null || properties.Count == 0)
        {
            return "[]";
        }

        // Reset the context and use deconstruct to get buffer and writer
        context.Reset();
        var (buffer, writer) = context;

        writer.WriteStartArray();
        for (int i = 0; i < properties.Count; i++)
        {
            var property = properties[i];
            writer.WriteStartObject();
            writer.WritePropertyName(NamePropertyName);
            writer.WriteStringValue(property.Name);
            writer.WritePropertyName(ValuePropertyName);
            writer.WriteStringValue(property.Value);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.Flush();
        var (array, count) = buffer.GetArray();
        return _encoder.GetString(array, 0, count);
    }

    internal static JsonWriterContext CreateWriter()
    {
        var context = new JsonWriterContext();
        return context;
    }

    public int CompareTo(StaticWebAssetEndpointProperty other) => string.CompareOrdinal(Name, other.Name) switch
    {
        0 => string.CompareOrdinal(Value, other.Value),
        int result => result
    };

    public override bool Equals(object obj) => obj is StaticWebAssetEndpointProperty endpointProperty &&
        Equals(endpointProperty);

    public bool Equals(StaticWebAssetEndpointProperty other) =>
       string.Equals(Name, other.Name, StringComparison.Ordinal) &&
       string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override int GetHashCode()
    {
#if NET472_OR_GREATER
        var hashCode = -244751520;
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Value);
        return hashCode;
#else
        return HashCode.Combine(Name, Value);
#endif
    }

    private string GetDebuggerDisplay() => $"Name: {Name}, Value: {Value}";
}
