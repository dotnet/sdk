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
    ///
    /// The OCI specification also defines sha512 and blake3 as optional
    /// registered algorithms. They are not included here because the rest of
    /// the containers pipeline (blob storage paths, digest creation, content
    /// verification) is currently SHA-256 only. Supporting additional
    /// algorithms requires changes across ContentStore, Layer, ImageBuilder,
    /// and the registry push/pull paths.
    /// </summary>
    private static readonly Dictionary<string, Regex> s_registeredAlgorithms = new(StringComparer.Ordinal)
    {
        ["sha256"] = new Regex(@"^[a-f0-9]{64}$"),
    };

    /// <summary>
    /// Computes the SHA-256 digest of <paramref name="content"/> and returns
    /// the full digest string (e.g. "sha256:abcdef...").
    /// </summary>
    internal static string ComputeSha256Digest(string content) => FormatSha256Digest(ComputeSha256(content));

    /// <summary>
    /// Formats a SHA-256 digest string from an already-computed encoded hash
    /// value.
    /// </summary>
    internal static string FormatSha256Digest(string encoded) => $"sha256:{encoded}";

    /// <summary>
    /// Validates a digest string against the OCI grammar and registered
    /// algorithms, then returns the encoded portion. The algorithm identifier
    /// is returned via <paramref name="algorithm"/>.
    /// </summary>
    internal static string GetEncoded(string digest)
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
    /// Computes the SHA-256 hash of <paramref name="content"/> and returns it
    /// as a lowercase hex string.
    /// </summary>
    internal static string ComputeSha256(string content)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(content), hash);
        return Convert.ToHexStringLower(hash);
    }
}
