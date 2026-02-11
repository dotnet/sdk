// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// URL-safe Base64 encoding/decoding helpers.
/// WebSocket subprotocol tokens cannot contain +, /, or = characters,
/// so we use URL-safe Base64 (RFC 4648 Section 5).
/// On .NET 8+ we delegate to the BCL implementation.
/// </summary>
internal static class Base64Url
{
#if NET8_0_OR_GREATER
    /// <summary>
    /// Encodes binary data to URL-safe Base64.
    /// </summary>
    internal static string EncodeToString(byte[] data)
        => System.Buffers.Text.Base64Url.EncodeToString(data);

    /// <summary>
    /// Encodes binary data to URL-safe Base64.
    /// </summary>
    internal static string EncodeToString(ReadOnlySpan<byte> data)
        => System.Buffers.Text.Base64Url.EncodeToString(data);

    /// <summary>
    /// Decodes URL-safe Base64 to binary data.
    /// </summary>
    internal static byte[] DecodeFromChars(string urlSafeBase64)
        => System.Buffers.Text.Base64Url.DecodeFromChars(urlSafeBase64);

    /// <summary>
    /// Converts URL-safe Base64 back to standard Base64.
    /// </summary>
    internal static string DecodeToStandardBase64(string urlSafeBase64)
        => Convert.ToBase64String(System.Buffers.Text.Base64Url.DecodeFromChars(urlSafeBase64));
#else
    /// <summary>
    /// Encodes binary data to URL-safe Base64.
    /// </summary>
    internal static string EncodeToString(byte[] data)
        => ToUrlSafe(Convert.ToBase64String(data));

    /// <summary>
    /// Decodes URL-safe Base64 to binary data.
    /// </summary>
    internal static byte[] DecodeFromChars(string urlSafeBase64)
        => Convert.FromBase64String(DecodeToStandardBase64(urlSafeBase64));

    /// <summary>
    /// Converts URL-safe Base64 back to standard Base64.
    /// </summary>
    internal static string DecodeToStandardBase64(string urlSafeBase64)
    {
        var standardBase64 = urlSafeBase64
            .Replace('-', '+')
            .Replace('_', '/');

        // Add padding if needed (Base64 length must be multiple of 4)
        var paddingNeeded = (4 - standardBase64.Length % 4) % 4;
        if (paddingNeeded > 0)
        {
            standardBase64 += new string('=', paddingNeeded);
        }

        return standardBase64;
    }

    private static string ToUrlSafe(string standardBase64)
    {
        return standardBase64
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
#endif
}
