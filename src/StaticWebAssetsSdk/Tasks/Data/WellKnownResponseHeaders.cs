// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

internal static class WellKnownResponseHeaders
{
    // Header names from the sample payload
    public const string AcceptRanges = "Accept-Ranges";
    public const string CacheControl = "Cache-Control";
    public const string ContentEncoding = "Content-Encoding";
    public const string ContentLength = "Content-Length";
    public const string ContentType = "Content-Type";
    public const string ETag = "ETag";
    public const string LastModified = "Last-Modified";
    public const string Vary = "Vary";

    // Pre-encoded property names for high-performance JSON serialization
    public static readonly JsonEncodedText AcceptRangesPropertyName = JsonEncodedText.Encode(AcceptRanges);
    public static readonly JsonEncodedText CacheControlPropertyName = JsonEncodedText.Encode(CacheControl);
    public static readonly JsonEncodedText ContentEncodingPropertyName = JsonEncodedText.Encode(ContentEncoding);
    public static readonly JsonEncodedText ContentLengthPropertyName = JsonEncodedText.Encode(ContentLength);
    public static readonly JsonEncodedText ContentTypePropertyName = JsonEncodedText.Encode(ContentType);
    public static readonly JsonEncodedText ETagPropertyName = JsonEncodedText.Encode(ETag);
    public static readonly JsonEncodedText LastModifiedPropertyName = JsonEncodedText.Encode(LastModified);
    public static readonly JsonEncodedText VaryPropertyName = JsonEncodedText.Encode(Vary);

    public static string TryGetInternedHeaderName(ReadOnlySpan<byte> headerNameSpan)
    {
        return headerNameSpan.Length switch
        {
            4 => headerNameSpan[0] switch
            {
                (byte)'E' when headerNameSpan.SequenceEqual("ETag"u8) => ETag,
                (byte)'V' when headerNameSpan.SequenceEqual("Vary"u8) => Vary,
                _ => null
            },
            12 => headerNameSpan.SequenceEqual("Content-Type"u8) ? ContentType : null,
            13 => headerNameSpan[0] switch
            {
                (byte)'A' when headerNameSpan.SequenceEqual("Accept-Ranges"u8) => AcceptRanges,
                (byte)'C' when headerNameSpan.SequenceEqual("Cache-Control"u8) => CacheControl,
                (byte)'L' when headerNameSpan.SequenceEqual("Last-Modified"u8) => LastModified,
                _ => null
            },
            14 => headerNameSpan.SequenceEqual("Content-Length"u8) ? ContentLength : null,
            16 => headerNameSpan.SequenceEqual("Content-Encoding"u8) ? ContentEncoding : null,
            _ => null
        };
    }

    public static JsonEncodedText? TryGetPreEncodedHeaderName(string name) =>
        name?.Length switch
        {
            4 => name[0] switch
            {
                'E' when string.Equals(name, ETag, StringComparison.Ordinal) => ETagPropertyName,
                'V' when string.Equals(name, Vary, StringComparison.Ordinal) => VaryPropertyName,
                _ => null
            },
            12 when string.Equals(name, ContentType, StringComparison.Ordinal) => ContentTypePropertyName,
            13 => name[0] switch
            {
                'A' when string.Equals(name, AcceptRanges, StringComparison.Ordinal) => AcceptRangesPropertyName,
                'C' when string.Equals(name, CacheControl, StringComparison.Ordinal) => CacheControlPropertyName,
                'L' when string.Equals(name, LastModified, StringComparison.Ordinal) => LastModifiedPropertyName,
                _ => null
            },
            14 when string.Equals(name, ContentLength, StringComparison.Ordinal) => ContentLengthPropertyName,
            16 when string.Equals(name, ContentEncoding, StringComparison.Ordinal) => ContentEncodingPropertyName,
            _ => null
        };
}
