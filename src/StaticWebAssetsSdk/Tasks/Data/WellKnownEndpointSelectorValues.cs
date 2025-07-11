// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

internal static class WellKnownEndpointSelectorValues
{
    // Common selector values for Content-Encoding
    public const string Gzip = "gzip";
    public const string Brotli = "br";

    /// <summary>
    /// Gets the interned selector value if it's a well-known value, otherwise returns null.
    /// Uses span comparison for efficiency.
    /// </summary>
    /// <param name="selectorValueSpan">The selector value span to check</param>
    /// <returns>The interned selector value or null if not well-known</returns>
    public static string TryGetInternedSelectorValue(ReadOnlySpan<byte> selectorValueSpan)
    {
        return (selectorValueSpan.Length switch
        {
            2 => selectorValueSpan.SequenceEqual("br"u8) ? Brotli : null,
            4 => selectorValueSpan.SequenceEqual("gzip"u8) ? Gzip : null,
            _ => null
        });
    }
}
