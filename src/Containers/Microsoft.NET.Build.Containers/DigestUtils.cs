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
    /// Validates a digest string against the OCI grammar and algorithms supported by this implementation.
    /// </summary>
    /// <remarks>
    /// Does not check the digest against any actual content.
    /// </remarks>
    /// <throws cref="InvalidDigestException">Thrown if the digest is invalid.</throws>
    internal static void ValidateSupportedDigestFormat(string digest, out string algorithm, out ReadOnlySpan<byte> encodedValue)
    {
        if (TryParseDigest(digest, out algorithm, out encodedValue, out DigestParseFailure parseFailure))
        {
            return;
        }

        string message = parseFailure switch
        {
            DigestParseFailure.InvalidFormat =>
                $"Digest '{digest}' does not match expected pattern '{ReferenceParser.AnchoredDigestRegexp}'.",
            DigestParseFailure.UnsupportedAlgorithm =>
                $"Unsupported digest algorithm '{algorithm}'. Supported algorithms: {string.Join(", ", s_registeredAlgorithms.Keys)}.",
            DigestParseFailure.InvalidEncodedValue =>
                $"Digest '{digest}' encoded value does not match expected pattern for algorithm '{algorithm}': '{s_registeredAlgorithms[algorithm]}'.",
            _ => throw new InvalidOperationException($"Unexpected digest parse failure '{parseFailure}'."),
        };

        throw new InvalidDigestException(message);
    }

    /// <summary>
    /// Validates that digest is correctly formatted and that it matches the
    /// digest of the provided content.
    /// </summary>
    /// <throws cref="InvalidDigestException">
    /// Thrown if the digest is invalid or does not match the content.
    /// </throws>
    internal static void ValidateDigestContent(string digest, ReadOnlySpan<byte> content)
    {
        ValidateSupportedDigestFormat(digest, out _, out _);
        string actualDigest = FormatSha256Digest(ComputeSha256(content));
        if (!string.Equals(actualDigest, digest, StringComparison.Ordinal))
        {
            throw new InvalidDigestException(
                $"Content does not match expected digest '{digest}'. Computed digest was '{actualDigest}'.");
        }
    }

    /// <summary>
    /// Validates a digest string against the OCI grammar and registered
    /// algorithms, then returns the encoded portion as a string.
    /// </summary>
    /// <remarks>
    /// <c>GetEncoded("sha256:e3b0c4...")</c> returns <c>"e3b0c4..."</c>.
    /// </remarks>
    internal static string GetEncoded(string digest)
    {
        ValidateSupportedDigestFormat(digest, out _, out ReadOnlySpan<byte> encoded);
        return Convert.ToHexStringLower(encoded);
    }

    /// <summary>
    /// Validates a digest string against the OCI grammar and registered
    /// algorithms, then returns the encoded portion as bytes.
    /// </summary>
    /// <remarks>
    /// <c>GetEncoded("sha256:e3b0c4...")</c> returns <c>"e3b0c4..."</c>.
    /// </remarks>
    internal static ReadOnlySpan<byte> GetEncodedValue(string digest)
    {
        ValidateSupportedDigestFormat(digest, out _, out ReadOnlySpan<byte> encodedValue);
        return encodedValue;
    }

    /// <summary>
    /// Computes the SHA-256 hash of <paramref name="content"/> and returns it
    /// as a lowercase hex string.
    /// </summary>
    /// <remarks>
    /// <c>ComputeSha256("")</c> returns <c>"e3b0c4..."</c>.
    /// </remarks>
    internal static string ComputeSha256(string content) => ComputeSha256(Encoding.UTF8.GetBytes(content));

    private static string ComputeSha256(ReadOnlySpan<byte> content)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(content, hash);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Validates hash value against the expected hash, failing with a
    /// consistent error message if they don't match.
    /// </summary>
    internal static void ValidateHashValueAsync(ReadOnlySpan<byte> actualHash, ReadOnlySpan<byte> expectedHash)
    {
        InvalidDigestException.ThrowIfMismatched(expectedHash, actualHash);
    }

    /// <summary>
    /// Attempts to parse a digest string against the OCI grammar and registered algorithms.
    /// </summary>
    /// <remarks>
    /// For "sha256:e3b0c4...", algorithm="sha256" and encoded="e3b0c4..."
    /// </remarks>
    internal static bool TryParseDigest(string digest, out string algorithm, out ReadOnlySpan<byte> encodedValue) =>
        TryParseDigest(digest, out algorithm, out encodedValue, out _);

    private static bool TryParseDigest(
        string digest,
        out string algorithm,
        out ReadOnlySpan<byte> encodedValue,
        out DigestParseFailure parseFailure)
    {
        algorithm = string.Empty;
        encodedValue = default;
        parseFailure = DigestParseFailure.None;

        Match match = ReferenceParser.AnchoredDigestRegexp.Match(digest);

        if (!match.Success)
        {
            parseFailure = DigestParseFailure.InvalidFormat;
            return false;
        }

        algorithm = match.Groups[1].Value;
        string encoded = match.Groups[2].Value;

        if (!s_registeredAlgorithms.TryGetValue(algorithm, out Regex? encodedPattern))
        {
            parseFailure = DigestParseFailure.UnsupportedAlgorithm;
            return false;
        }

        if (!encodedPattern.IsMatch(encoded))
        {
            parseFailure = DigestParseFailure.InvalidEncodedValue;
            return false;
        }
        encodedValue = Convert.FromHexString(encoded);
        return true;
    }

    private enum DigestParseFailure
    {
        None,
        InvalidFormat,
        UnsupportedAlgorithm,
        InvalidEncodedValue,
    }
}
