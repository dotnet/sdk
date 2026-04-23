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
        // TODO: Use GeneratedRegexAttribute when
        // https://github.com/dotnet/sdk/pull/53547 is merged.
        ["sha256"] = new Regex(@"^[a-f0-9]{64}$"),
    };

    /// <summary>
    /// Computes the SHA-256 digest of <paramref name="content"/> and returns
    /// the full digest string.
    /// </summary>
    /// <remarks>
    /// <c>ComputeSha256Digest("")</c> returns <c>"sha256:e3b0c4..."</c>.
    /// </remarks>
    internal static string ComputeSha256Digest(string content) => FormatSha256Digest(ComputeSha256(content));

    /// <summary>
    /// Formats a SHA-256 digest string from an already-computed encoded hash
    /// value. The encoded value is lowercased to conform to the OCI spec.
    /// Throws <see cref="ArgumentException"/> if <paramref name="encoded"/>
    /// is not exactly 64 hex characters.
    /// </summary>
    /// <remarks>
    /// <c>FormatSha256Digest("abcdef...")</c> returns
    /// <c>"sha256:abcdef..."</c>.
    /// </remarks>
    internal static string FormatSha256Digest(string encoded)
    {
        encoded = encoded.ToLowerInvariant();

        if (!s_registeredAlgorithms["sha256"].IsMatch(encoded))
        {
            throw new ArgumentException(
                message: $"SHA-256 value '{encoded}' does not match expected format '{s_registeredAlgorithms["sha256"]}'",
                paramName: nameof(encoded));
        }

        return $"sha256:{encoded}";
    }

    /// <summary>
    /// Validates a digest string against the OCI grammar and registered
    /// algorithms. Throws <see cref="InvalidDigestException"/> if the digest
    /// is invalid.
    /// </summary>
    /// <remarks>
    /// <c>ValidateDigest("sha256:e3b0c4...")</c> succeeds.
    /// </remarks>
    internal static void ValidateDigest(string digest) => ValidateAndParseDigest(digest, out _, out _);

    /// <summary>
    /// Validates a digest string against the OCI grammar and registered
    /// algorithms, then returns the encoded portion.
    /// </summary>
    /// <remarks>
    /// <c>GetEncoded("sha256:e3b0c4...")</c> returns <c>"e3b0c4..."</c>.
    /// </remarks>
    internal static string GetEncoded(string digest)
    {
        ValidateAndParseDigest(digest, out _, out string encoded);
        return encoded;
    }

    /// <summary>
    /// Computes the SHA-256 hash of <paramref name="content"/> and returns it
    /// as a lowercase hex string.
    /// </summary>
    /// <remarks>
    /// <c>ComputeSha256("")</c> returns <c>"e3b0c4..."</c>.
    /// </remarks>
    internal static string ComputeSha256(string content)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(content), hash);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Validates a digest string against the OCI grammar and registered
    /// algorithms, returning the parsed algorithm and encoded portions. Throws
    /// <see cref="InvalidDigestException"/> if the digest is malformed, uses an
    /// unsupported algorithm, or the encoded value does not match the
    /// algorithm's expected format.
    /// </summary>
    /// <remarks>
    /// <c>ValidateAndParseDigest("sha256:e3b0c4...", out algorithm, out encoded)</c>
    /// sets <c>algorithm</c> to <c>"sha256"</c> and <c>encoded</c> to <c>"e3b0c4..."</c>.
    /// </remarks>
    private static void ValidateAndParseDigest(string digest, out string algorithm, out string encoded)
    {
        Match match = ReferenceParser.AnchoredDigestRegexp.Match(digest);

        if (!match.Success)
        {
            throw new InvalidDigestException(
                $"Digest '{digest}' does not match expected pattern '{ReferenceParser.AnchoredDigestRegexp}'.");
        }

        algorithm = match.Groups[1].Value;
        encoded = match.Groups[2].Value;

        if (!s_registeredAlgorithms.TryGetValue(algorithm, out Regex? encodedPattern))
        {
            string supportedAlgorithms = string.Join(", ", s_registeredAlgorithms.Keys);
            throw new InvalidDigestException(
                $"Unsupported digest algorithm '{algorithm}'. Supported algorithms: {supportedAlgorithms}.");
        }

        if (!encodedPattern.IsMatch(encoded))
        {
            throw new InvalidDigestException(
                $"Digest '{digest}' encoded value does not match expected pattern for algorithm '{algorithm}': '{encodedPattern}'.");
        }
    }
}
