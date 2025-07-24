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
public struct StaticWebAssetEndpointSelector : IEquatable<StaticWebAssetEndpointSelector>, IComparable<StaticWebAssetEndpointSelector>
{
    private static readonly JsonTypeInfo<StaticWebAssetEndpointSelector[]> _jsonTypeInfo =
        StaticWebAssetsJsonSerializerContext.Default.StaticWebAssetEndpointSelectorArray;

    private static readonly Encoding _encoder = Encoding.UTF8;

    // Pre-encoded property names for high-performance serialization
    private static readonly JsonEncodedText NamePropertyName = JsonEncodedText.Encode("Name");
    private static readonly JsonEncodedText ValuePropertyName = JsonEncodedText.Encode("Value");
    private static readonly JsonEncodedText QualityPropertyName = JsonEncodedText.Encode("Quality");

    public string Name { get; set; }

    public string Value { get; set; }

    public string Quality { get; set; }

    public static StaticWebAssetEndpointSelector[] FromMetadataValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        var result = JsonSerializer.Deserialize(value, _jsonTypeInfo);
        Array.Sort(result);
        return result;
    }

    public static void PopulateFromMetadataValue(string value, List<StaticWebAssetEndpointSelector> selectors)
    {
        selectors.Clear();

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
        byte[] bytes = rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
        var actualByteCount = _encoder.GetBytes(value, 0, value.Length, bytes, 0);
        var reader = new Utf8JsonReader(bytes.AsSpan(0, actualByteCount));
#endif

        try
        {
            reader.Read(); // Move to start array
            PopulateFromMetadataValue(ref reader, selectors);
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }

    public static void PopulateFromMetadataValue(ref Utf8JsonReader reader, List<StaticWebAssetEndpointSelector> selectors)
    {
        // Expect to be positioned at start of array
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Read(); // Move to start array if not already there
        }
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var selector = new StaticWebAssetEndpointSelector();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.ValueTextEquals("Name"u8))
                {
                    reader.Read();
                    // Try to get interned selector name first using span comparison
                    var internedName = WellKnownEndpointSelectorNames.TryGetInternedSelectorName(reader.ValueSpan);
                    selector.Name = internedName ?? reader.GetString();
                }
                else if (reader.ValueTextEquals("Value"u8))
                {
                    reader.Read();
                    // Try to get interned selector value first using span comparison
                    var internedValue = WellKnownEndpointSelectorValues.TryGetInternedSelectorValue(reader.ValueSpan);
                    selector.Value = internedValue ?? reader.GetString();
                }
                else if (reader.ValueTextEquals("Quality"u8))
                {
                    reader.Read();
                    selector.Quality = reader.GetString();
                }
                else
                {
                    reader.Skip();
                }
            }

            selectors.Add(selector);
        }
    }

    public static string ToMetadataValue(StaticWebAssetEndpointSelector[] selectors)
    {
        var sortedSelectors = selectors ?? [];
        Array.Sort(sortedSelectors);
        return JsonSerializer.Serialize(sortedSelectors, _jsonTypeInfo);
    }

    internal static string ToMetadataValue(
        List<StaticWebAssetEndpointSelector> selectors,
        JsonWriterContext context)
    {
        if (selectors == null || selectors.Count == 0)
        {
            return "[]";
        }

        // Reset the context and use deconstruct to get buffer and writer
        context.Reset();
        var (buffer, writer) = context;

        writer.WriteStartArray();
        for (int i = 0; i < selectors.Count; i++)
        {
            var selector = selectors[i];
            writer.WriteStartObject();
            writer.WritePropertyName(NamePropertyName);
            writer.WriteStringValue(selector.Name);
            writer.WritePropertyName(ValuePropertyName);
            writer.WriteStringValue(selector.Value);
            writer.WritePropertyName(QualityPropertyName);
            writer.WriteStringValue(selector.Quality);
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

    public int CompareTo(StaticWebAssetEndpointSelector other)
    {
        var nameComparison = string.CompareOrdinal(Name, other.Name);
        if (nameComparison != 0)
        {
            return nameComparison;
        }

        var valueComparison = string.CompareOrdinal(Value, other.Value);
        if (valueComparison != 0)
        {
            return valueComparison;
        }

        return 0;
    }

    public override bool Equals(object obj) => obj is StaticWebAssetEndpointSelector endpointSelector &&
        Equals(endpointSelector);

    public bool Equals(StaticWebAssetEndpointSelector other) =>
        string.Equals(Name, other.Name, StringComparison.Ordinal) &&
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override int GetHashCode()
    {
#if NET471_OR_GREATER
        var hashCode = 1379895590;
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Value);
        hashCode = hashCode * -1521134295 + Quality.GetHashCode();
        return hashCode;
#else
        return HashCode.Combine(Name, Value, Quality);
#endif
    }

    private string GetDebuggerDisplay() => $"Name: {Name}, Value: {Value}, Quality: {Quality}";
}
