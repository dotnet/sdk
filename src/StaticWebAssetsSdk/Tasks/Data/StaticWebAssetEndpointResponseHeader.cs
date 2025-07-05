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
public struct StaticWebAssetEndpointResponseHeader : IEquatable<StaticWebAssetEndpointResponseHeader>, IComparable<StaticWebAssetEndpointResponseHeader>
{
    private static readonly JsonTypeInfo<StaticWebAssetEndpointResponseHeader[]> _jsonTypeInfo =
        StaticWebAssetsJsonSerializerContext.Default.StaticWebAssetEndpointResponseHeaderArray;

    private static readonly Encoding _encoder = Encoding.UTF8;

    // Pre-encoded property names for high-performance serialization
    private static readonly JsonEncodedText NamePropertyName = JsonEncodedText.Encode("Name");
    private static readonly JsonEncodedText ValuePropertyName = JsonEncodedText.Encode("Value");

    public string Name { get; set; }

    public string Value { get; set; }

    public static StaticWebAssetEndpointResponseHeader[] FromMetadataValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        var result = JsonSerializer.Deserialize(value, _jsonTypeInfo);
        Array.Sort(result);
        return result;
    }

    public static void PopulateFromMetadataValue(string value, List<StaticWebAssetEndpointResponseHeader> headers)
    {
        headers.Clear();

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
        var bytes = rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
        var actualByteCount = _encoder.GetBytes(value, 0, value.Length, bytes, 0);
        var reader = new Utf8JsonReader(bytes.AsSpan(0, actualByteCount));
#endif

        try
        {
            PopulateFromMetadataValue(ref reader, headers);
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }

    public static void PopulateFromMetadataValue(ref Utf8JsonReader reader, List<StaticWebAssetEndpointResponseHeader> headers)
    {
        // Expect to be positioned at start of array
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Read(); // Move to start array if not already there
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var header = new StaticWebAssetEndpointResponseHeader();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.ValueTextEquals("Name"u8))
                {
                    reader.Read();
                    // Try to get interned header name first using span comparison
                    var internedName = WellKnownResponseHeaders.TryGetInternedHeaderName(reader.ValueSpan);
                    header.Name = internedName ?? reader.GetString();
                }
                else if (reader.ValueTextEquals("Value"u8))
                {
                    reader.Read();
                    // Try to get interned header value first using span comparison
                    var internedValue = WellKnownResponseHeaderValues.TryGetInternedHeaderValue(reader.ValueSpan);
                    header.Value = internedValue ?? reader.GetString();
                }
                else
                {
                    reader.Skip();
                }
            }

            headers.Add(header);
        }
    }

    internal static string ToMetadataValue(StaticWebAssetEndpointResponseHeader[] responseHeaders)
    {
        var headers = responseHeaders ?? [];
        Array.Sort(headers);
        return JsonSerializer.Serialize(headers, _jsonTypeInfo);
    }

    internal static string ToMetadataValue(
        List<StaticWebAssetEndpointResponseHeader> headers,
        JsonWriterContext context)
    {
        if (headers == null || headers.Count == 0)
        {
            return "[]";
        }

        // Reset the context and use deconstruct to get buffer and writer
        context.Reset();
        var (buffer, writer) = context;

        writer.WriteStartArray();
        for (var i = 0; i < headers.Count; i++)
        {
            var header = headers[i];
            writer.WriteStartObject();
            writer.WritePropertyName(NamePropertyName);
            var preEncoded = WellKnownResponseHeaders.TryGetPreEncodedHeaderName(header.Name);
            if (preEncoded.HasValue)
            {
                writer.WriteStringValue(preEncoded.Value);
            }
            else
            {
                writer.WriteStringValue(header.Name);
            }
            writer.WritePropertyName(ValuePropertyName);
            writer.WriteStringValue(header.Value);
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

    private string GetDebuggerDisplay() => $"{Name}: {Value}";

    public override bool Equals(object obj) => obj is StaticWebAssetEndpointResponseHeader responseHeader &&
        Equals(responseHeader);

    public bool Equals(StaticWebAssetEndpointResponseHeader other) => string.Equals(Name, other.Name, StringComparison.Ordinal) &&
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

    public int CompareTo(StaticWebAssetEndpointResponseHeader other) => string.CompareOrdinal(Name, other.Name) switch
    {
        0 => string.CompareOrdinal(Value, other.Value),
        int result => result
    };
}
