// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable IDE0240
#nullable enable
#pragma warning restore IDE0240

using System.Buffers;
using System.Text.Json;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public partial class WorkloadManifestReader
    {
        public static WorkloadManifest ReadWorkloadManifest(string manifestId, Stream manifestStream, Stream? localizationStream, string manifestPath)
        {
            var readerOptions = new JsonReaderOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };

            var localizationCatalog = ReadLocalizationCatalog(localizationStream, readerOptions);
            var manifestReader = new Utf8JsonStreamReader(manifestStream, readerOptions);

            return ReadWorkloadManifest(manifestId, manifestPath, localizationCatalog, ref manifestReader);
        }

        private static LocalizationCatalog? ReadLocalizationCatalog(Stream? localizationStream, JsonReaderOptions readerOptions)
        {
            if (localizationStream == null)
            {
                return null;
            }

            var localizationReader = new Utf8JsonStreamReader(localizationStream, readerOptions);
            return ReadLocalizationCatalog(ref localizationReader);
        }

        internal ref struct Utf8JsonStreamReader
        {
            static ReadOnlySpan<byte> utf8Bom => new byte[] { 0xEF, 0xBB, 0xBF };

            const int segmentSize = 4096;

            Utf8JsonReader reader;
            readonly Stream stream;

            byte[]? buffer;

            Span<byte> span;

            public Utf8JsonStreamReader(Stream stream, JsonReaderOptions readerOptions)
            {
                this.stream = stream;

                buffer = ArrayPool<byte>.Shared.Rent(segmentSize);
                var readCount = stream.Read(buffer, 0, buffer.Length);
                span = buffer.AsSpan().Slice(0, readCount);

                if (span.StartsWith(utf8Bom))
                {
                    span = span.Slice(utf8Bom.Length, span.Length - utf8Bom.Length);
                }

                reader = new Utf8JsonReader(span, readCount >= stream.Length, new JsonReaderState(readerOptions));
            }

            public bool Read()
            {
                while (!reader.Read())
                {
                    if (reader.IsFinalBlock)
                    {
                        if (buffer != null)
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                            buffer = null;
                        }
                        return false;
                    }

                    var newSegmentSize = segmentSize;

                    // if the value was too big to fit in the buffer, get a bigger buffer
                    if (reader.BytesConsumed == span.Length)
                    {
                        newSegmentSize = span.Length * 2;
                    }

                    int remaining = (int)(span.Length - reader.BytesConsumed);

                    var newBuffer = ArrayPool<byte>.Shared.Rent(newSegmentSize);

                    if (remaining > 0)
                    {
                        span.Slice((int)reader.BytesConsumed).CopyTo(newBuffer);
                    }

                    var readCount = stream.Read(newBuffer, remaining, newBuffer.Length - remaining);

                    if (buffer != null)
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                    buffer = newBuffer;
                    span = newBuffer.AsSpan().Slice(0, remaining + readCount);

                    reader = new Utf8JsonReader(span, stream.Position >= stream.Length, reader.CurrentState);
                }

                return true;
            }

            public long TokenStartIndex => reader.TokenStartIndex;

            public JsonTokenType TokenType => reader.TokenType;

            public int CurrentDepth => reader.CurrentDepth;

            public string? GetString() => reader.GetString();
            public bool TryGetInt64(out long value) => reader.TryGetInt64(out value);
            public bool GetBool() => reader.GetBoolean();
        }
    }

    internal static class JsonTokenTypeExtensions
    {
        public static bool IsBool(this JsonTokenType tokenType) => tokenType == JsonTokenType.True || tokenType == JsonTokenType.False;
        public static bool IsInt(this JsonTokenType tokenType) => tokenType == JsonTokenType.Number;
    }
}
