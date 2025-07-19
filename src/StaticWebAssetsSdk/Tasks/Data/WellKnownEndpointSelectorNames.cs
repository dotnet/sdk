// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

internal static class WellKnownEndpointSelectorNames
{
    // Common endpoint selector names
    public const string ContentEncoding = "Content-Encoding";

    /// <summary>
    /// Gets the interned selector name if it's a well-known selector name, otherwise returns null.
    /// Uses span comparison for efficiency.
    /// </summary>
    /// <param name="selectorNameSpan">The selector name span to check</param>
    /// <returns>The interned selector name or null if not well-known</returns>
    public static string TryGetInternedSelectorName(ReadOnlySpan<byte> selectorNameSpan)
    {
        return (selectorNameSpan.Length switch
        {
            16 => selectorNameSpan.SequenceEqual("Content-Encoding"u8) ? ContentEncoding : null,
            _ => null
        });
    }
}
