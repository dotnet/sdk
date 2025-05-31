// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.NET.Build.Containers;

internal sealed class DigestUtils
{
    /// <summary>
    /// UTF8 encoding without BOM.
    /// </summary>
    internal static Encoding UTF8 = new UTF8Encoding(false);

    /// <summary>
    /// Gets digest for string <paramref name="str"/>.
    /// </summary>
    internal static string GetDigest<T>(T content) => GetDigestFromSha(GetSha(content));

    /// <summary>
    /// Formats digest based on ready SHA <paramref name="sha"/>.
    /// </summary>
    internal static string GetDigestFromSha(string sha) => $"sha256:{sha}";

    internal static string GetShaFromDigest(string digest)
    {
        if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Invalid digest '{digest}'. Digest must start with 'sha256:'.");
        }

        return digest.Substring("sha256:".Length);
    }

    /// <summary>
    /// Gets the SHA of <paramref name="str"/>.
    /// </summary>
    internal static string GetSha(string str)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        var bytes = UTF8.GetBytes(str);
        SHA256.HashData(bytes, hash);

        return Convert.ToHexStringLower(hash);
    }

    internal static string GetSha<T>(T content)
    {
        var jsonstring = JsonSerializer.Serialize(content);
        return GetSha(jsonstring);
    }

    internal static long GetUtf8Length(string content) => UTF8.GetBytes(content).LongLength;
}
