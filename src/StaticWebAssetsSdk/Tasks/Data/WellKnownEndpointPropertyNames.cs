// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

internal static class WellKnownEndpointPropertyNames
{
    // Common endpoint property names
    public const string Label = "label";
    public const string Integrity = "integrity";

    /// <summary>
    /// Gets the interned property name if it's a well-known property name, otherwise returns null.
    /// Uses span comparison for efficiency.
    /// </summary>
    /// <param name="propertyNameSpan">The property name span to check</param>
    /// <returns>The interned property name or null if not well-known</returns>
    public static string TryGetInternedPropertyName(ReadOnlySpan<byte> propertyNameSpan)
    {
        return propertyNameSpan.Length switch
        {
            5 => propertyNameSpan.SequenceEqual("label"u8) ? Label : null,
            9 => propertyNameSpan.SequenceEqual("integrity"u8) ? Integrity : null,
            _ => null
        };
    }
}
