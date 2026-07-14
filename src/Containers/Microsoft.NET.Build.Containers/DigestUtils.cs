// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;

namespace Microsoft.NET.Build.Containers;

internal sealed class DigestUtils
{
    /// <summary>
    /// Gets digest for string <paramref name="str"/>.
    /// </summary>
    internal static string GetDigest(string str) => GetDigestFromSha(GetSha(str));

    /// <summary>
    /// Formats digest based on ready SHA <paramref name="sha"/>.
    /// </summary>
    internal static string GetDigestFromSha(string sha) => $"sha256:{sha}";

    /// <summary>
    /// Gets the SHA of <paramref name="str"/>.
    /// </summary>
    internal static string GetSha(string str)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(str), hash);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Returns the encoded (hash) portion of a digest string as raw bytes.
    /// </summary>
    /// <remarks>
    /// <c>GetEncodedValue("sha256:e3b0c4...")</c> returns the bytes for <c>"e3b0c4..."</c>.
    /// </remarks>
    internal static byte[] GetEncodedValue(string digest)
    {
        int separatorIndex = digest.IndexOf(':');
        string encoded = separatorIndex >= 0 ? digest[(separatorIndex + 1)..] : digest;
        return Convert.FromHexString(encoded);
    }
}
