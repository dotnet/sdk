// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Microsoft.NET.Build.Containers;

internal sealed class DigestUtils
{
    /// <summary>
    /// The set of registered algorithm identifiers from the OCI image-spec,
    /// mapped to the regex pattern that the encoded portion of the digest must
    /// match. Taken directly from the OCI image specification:
    /// <see href="https://github.com/opencontainers/image-spec/blob/a4c6ade7bb82b316d45391f572727a63e268b252/descriptor.md#registered-algorithms">
    /// Registered Algorithms
    /// </see>
    /// </summary>
    private static readonly Dictionary<string, Regex> s_registeredAlgorithms = new(StringComparer.Ordinal)
    {
        ["sha256"] = new Regex(@"^[a-f0-9]{64}$"),
        ["sha512"] = new Regex(@"^[a-f0-9]{128}$"),
        ["blake3"] = new Regex(@"^[a-f0-9]{64}$")
    };

    /// <summary>
    /// Gets digest for string <paramref name="str"/>.
    /// </summary>
    internal static string GetDigest(string str) => GetDigestFromSha(GetSha(str));

    /// <summary>
    /// Formats digest based on ready SHA <paramref name="sha"/>.
    /// </summary>
    internal static string GetDigestFromSha(string sha) => $"sha256:{sha}";

    internal static string GetShaFromDigest(string digest)
    {
        Match match = ReferenceParser.AnchoredDigestRegexp.Match(digest);

        if (!match.Success)
        {
            throw new ArgumentException($"Invalid digest '{digest}'.");
        }

        string algorithm = match.Groups[1].Value;
        string encoded = match.Groups[2].Value;

        if (!s_registeredAlgorithms.TryGetValue(algorithm, out Regex? encodedPattern))
        {
            string supportedAlgorithms = string.Join(", ", s_registeredAlgorithms.Keys);
            throw new ArgumentException(
                $"Unsupported digest algorithm '{algorithm}'. Supported algorithms: {supportedAlgorithms}.");
        }

        if (!encodedPattern.IsMatch(encoded))
        {
            throw new ArgumentException($"Invalid encoded digest value for algorithm '{algorithm}'.");
        }

        return encoded;
    }

    /// <summary>
    /// Gets the SHA of <paramref name="str"/>.
    /// </summary>
    internal static string GetSha(string str)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(str), hash);

        return Convert.ToHexStringLower(hash);
    }
}
