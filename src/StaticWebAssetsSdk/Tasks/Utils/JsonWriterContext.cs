// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Text.Json;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;

/// <summary>
/// A reusable context for high-performance JSON writing using pooled buffers.
/// This struct encapsulates a PooledArrayBufferWriter and Utf8JsonWriter to eliminate
/// allocations during repeated JSON serialization operations.
/// </summary>
internal struct JsonWriterContext : IDisposable
{
    internal static readonly JsonWriterOptions WriterOptions = new JsonWriterOptions
    {
        SkipValidation = true,
        Indented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public PooledArrayBufferWriter<byte> Buffer { get; private set; }
    public Utf8JsonWriter Writer { get; private set; }

    /// <summary>
    /// Resets the context for reuse, creating the buffer and writer if needed
    /// and clearing any existing content.
    /// </summary>
    public void Reset()
    {
        Buffer ??= new PooledArrayBufferWriter<byte>();
        Writer ??= new Utf8JsonWriter(Buffer, WriterOptions);
        Buffer.Clear();
        Writer.Reset(Buffer);
    }

    /// <summary>
    /// Deconstructs the context into its buffer and writer components.
    /// </summary>
    public void Deconstruct(out PooledArrayBufferWriter<byte> buffer, out Utf8JsonWriter writer)
    {
        buffer = Buffer;
        writer = Writer;
    }

    /// <summary>
    /// Disposes the writer and buffer resources.
    /// </summary>
    public void Dispose()
    {
        Writer?.Dispose();
        Buffer?.Dispose();
    }
}
