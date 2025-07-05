// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

internal static class WellKnownResponseHeaderValues
{
    // Accept-Ranges values
    public const string Bytes = "bytes";

    // Cache-Control values
    public const string NoCache = "no-cache";
    public const string MaxAgeImmutable = "max-age=31536000, immutable";

    // Content-Encoding values
    public const string Gzip = "gzip";
    public const string Brotli = "br";

    // Content-Type values (common ones)
    public const string ApplicationOctetStream = "application/octet-stream";
    public const string TextJavascript = "text/javascript";
    public const string TextCss = "text/css";
    public const string TextHtml = "text/html";
    public const string ApplicationJson = "application/json";
    public const string ImagePng = "image/png";
    public const string ImageJpeg = "image/jpeg";
    public const string ImageSvg = "image/svg+xml";

    // Vary values
    public const string ContentEncoding = "Content-Encoding";
    /// <summary>
    /// Gets the interned header value if it's a well-known value, otherwise returns the original string.
    /// Uses span comparison for efficiency.
    /// </summary>
    /// <param name="headerValueSpan">The header value span to check</param>
    /// <returns>The interned header value or null if not well-known</returns>
    public static string TryGetInternedHeaderValue(ReadOnlySpan<byte> headerValueSpan)
    {
        return headerValueSpan.Length switch
        {
            2 => headerValueSpan.SequenceEqual("br"u8) ? Brotli : null,
            4 => headerValueSpan.SequenceEqual("gzip"u8) ? Gzip : null,
            5 => headerValueSpan.SequenceEqual("bytes"u8) ? Bytes : null,
            8 => headerValueSpan[0] switch
            {
                (byte)'n' when headerValueSpan.SequenceEqual("no-cache"u8) => NoCache,
                (byte)'t' when headerValueSpan.SequenceEqual("text/css"u8) => TextCss,
                _ => null
            },
            9 => headerValueSpan[0] switch
            {
                (byte)'t' when headerValueSpan.SequenceEqual("text/html"u8) => TextHtml,
                (byte)'i' when headerValueSpan.SequenceEqual("image/png"u8) => ImagePng,
                _ => null
            },
            10 => headerValueSpan.SequenceEqual("image/jpeg"u8) ? ImageJpeg : null,
            13 => headerValueSpan.SequenceEqual("image/svg+xml"u8) ? ImageSvg : null,
            15 => headerValueSpan.SequenceEqual("text/javascript"u8) ? TextJavascript : null,
            16 => headerValueSpan[0] switch
            {
                (byte)'a' when headerValueSpan.SequenceEqual("application/json"u8) => ApplicationJson,
                (byte)'C' when headerValueSpan.SequenceEqual("Content-Encoding"u8) => ContentEncoding,
                _ => null
            },
            24 => headerValueSpan.SequenceEqual("application/octet-stream"u8) ? ApplicationOctetStream : null,
            27 => headerValueSpan.SequenceEqual("max-age=31536000, immutable"u8) ? MaxAgeImmutable : null,
            _ => null
        };
    }
}
