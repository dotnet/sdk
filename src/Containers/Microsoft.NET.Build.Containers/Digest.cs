// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// A string that pairs a hashing algorithm with a value, used to identify content uniquely.
/// </summary>
[JsonConverter(typeof(DigestConverter))]
public readonly record struct Digest
{
    public DigestAlgorithm Algorithm { get; }
    public string Value { get; }

    internal Digest(DigestAlgorithm algorithm, string encodedValue)
    {
        Algorithm = algorithm;
        Value = encodedValue;
        algorithm.Validate(encodedValue);
    }
    /// <summary>
    /// Returns the digest as a string in the format "algorithm:value".
    /// </summary>
    public override string ToString() => $"{Algorithm.ToString().ToLowerInvariant()}:{Value}";

    public static Digest Parse(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("Value cannot be null or empty.", nameof(value));
        }

        string[] parts = value.Split(':');
        if (parts.Length != 2)
        {
            throw new FormatException($"Invalid digest format: '{value}'. Expected format is 'algorithm:value'.");
        }

        if (!Enum.TryParse(parts[0], ignoreCase: true, out DigestAlgorithm algorithm))
        {
            throw new FormatException($"Unknown digest algorithm: '{parts[0]}'.");
        }

        return new Digest(algorithm, parts[1]);
    }

    /// <summary>
    /// Creates a <see cref="Digest"/> from a string that does not specify an algorithm.
    /// The value _must_ be a valid SHA256 hash value.
    /// </summary>
    /// <param name="encodedValue">A SHA256 hash</param>
    /// <returns>A Digest with the SHA256 algorithm and the <paramref name="encodedValue"/></returns>
    public static Digest FromUnmarkedString(string encodedValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(encodedValue);
        return new Digest(CanonicalAlgorithm.Canonical, encodedValue);
    }

    /// <summary>
    /// Creates a <see cref="Digest"/> by hashing an input string with a specified algorithm.
    /// </summary>
    public static Digest FromContentString(DigestAlgorithm algorithm, string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value), "Input value cannot be null.");
        }
        var (_contentLength, hashedValue) = algorithm.HashInput(value);
        return new Digest(algorithm, hashedValue);
    }

    /// <summary>
    /// Creates a <see cref="Digest"/> by hashing an object with a specified algorithm.
    /// The object is serialized to JSON before hashing.
    /// </summary>
    public static Digest FromContent<T>(DigestAlgorithm algorithm, T content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var jsonString = JsonSerializer.Serialize(content);
        return FromContentString(algorithm, jsonString);
    }

    public static async Task<Digest> FromStream(DigestAlgorithm algorithm, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        }

       Func<Stream, CancellationToken, ValueTask<byte[]>> hasher = algorithm switch
        {
            DigestAlgorithm.sha256 => (s, ct) => SHA256.HashDataAsync(s, ct),
            DigestAlgorithm.sha512 => (s, ct) => SHA512.HashDataAsync(s, ct),
            _ => throw new ArgumentException(nameof(algorithm))
        };

        byte[] hash = await hasher(stream, cancellationToken).ConfigureAwait(false);
        string encodedValue = Convert.ToHexString(hash).ToLowerInvariant();
        return new Digest(algorithm, encodedValue);
    }
}

internal class DigestConverter : JsonConverter<Digest>
{
    public override Digest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string[] parts = reader.GetString()!.Split(':');
        if (parts.Length != 2)
        {
            throw new JsonException("Invalid digest format. Expected 'algorithm:value'.");
        }
        if (!Enum.TryParse(parts[0], ignoreCase: false, out DigestAlgorithm algorithm))
        {
            throw new JsonException($"Unknown digest algorithm: {parts[0]}.");
        }

        return new Digest(algorithm, parts[1]);
    }

    public override void Write(Utf8JsonWriter writer, Digest value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<DigestAlgorithm>))]
public enum DigestAlgorithm
{
    /// <summary>
    /// SHA256 hashing algorithm.
    /// </summary>
    sha256,

    /// <summary>
    /// SHA512 hashing algorithm.
    /// </summary>
    sha512
}

internal static class CanonicalAlgorithm
{
    /// <summary>
    /// The default digest algorithm used for otherwise-unmarked digest values.
    /// </summary>
    internal static DigestAlgorithm Canonical => DigestAlgorithm.sha256;
}

internal static partial class DigestAlgorithmExtensions
{
    /// <summary>
    /// UTF8 encoding without BOM.
    /// </summary>
    internal static Encoding UTF8NoBom = new UTF8Encoding(false);

    public static void Validate(this DigestAlgorithm algorithm, string encodedValue)
    {
        if (algorithm == DigestAlgorithm.sha256 && encodedValue.Length != 64)
        {
            throw new ArgumentException("SHA256 digest must be 64 characters long.", nameof(encodedValue));
        }
        else if (algorithm == DigestAlgorithm.sha512 && encodedValue.Length != 128)
        {
            throw new ArgumentException("SHA512 digest must be 128 characters long.", nameof(encodedValue));
        }
        // and the value string must be hex-encoded
        if (!MyRegex().IsMatch(encodedValue))
        {
            throw new ArgumentException("Digest value must be a valid lowercase hexadecimal string.", nameof(encodedValue));
        }
    }

    public static (long contentLength, string contentHash) HashInput(this DigestAlgorithm algorithm, string input)
    {
        ArgumentException.ThrowIfNullOrEmpty(input);
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        var bytes = UTF8NoBom.GetBytes(input);

        switch (algorithm)
        {
            case DigestAlgorithm.sha256:
                SHA256.HashData(bytes, hash);
                break;
            case DigestAlgorithm.sha512:
                SHA512.HashData(bytes, hash);
                break;
            default:
                throw new NotSupportedException($"Unsupported digest algorithm: {algorithm}.");
        };
        return (bytes.LongLength, Convert.ToHexString(hash));
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^[0-9a-f]+$")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}
