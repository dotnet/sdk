// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Security.Cryptography;
using System.Text.Json;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;

/// <summary>
/// Reads and writes the two JSON documents that describe the <c>dotnet watch</c> browser tools key
/// pair. The public document is the only half that is ever pinned into the application build output;
/// the private document is consumed exclusively by <c>dotnet watch</c> to key its provider.
///
/// Both documents are versioned so that a key produced by an older SDK is treated as invalid rather
/// than misinterpreted, and both carry the same base64 X.509 SubjectPublicKeyInfo value so that a
/// mismatch between the two files can be detected without deriving anything.
///
/// The representation of the private half is the raw <see cref="RSAParameters"/> components rather
/// than PKCS#8 because the task runs on .NET Framework as well, where the PKCS#8 import and export
/// APIs are not available.
/// </summary>
internal static class BrowserToolsKeyPair
{
    public const int CurrentVersion = 1;

    /// <summary>
    /// The padding the browser uses to encrypt the shared secret and the provider uses to decrypt
    /// it. Recorded so that a future change of algorithm invalidates existing key files.
    /// </summary>
    public const string Algorithm = "RSA-OAEP-SHA256";

    public const string PublicKeyFormat = "SubjectPublicKeyInfo";
    public const string PrivateKeyFormat = "RSAParameters";

    public const int KeySizeInBits = 2048;

    /// <summary>
    /// Result of reading a key document. <see cref="IsValid"/> is false for anything that is not a
    /// well formed, current version document; the caller regenerates in that case. The failure
    /// reason never contains key material.
    /// </summary>
    public readonly struct ReadResult(bool isValid, string publicKey, RSAParameters parameters, string reason)
    {
        public bool IsValid { get; } = isValid;
        public string PublicKey { get; } = publicKey;
        public RSAParameters Parameters { get; } = parameters;
        public string Reason { get; } = reason;

        public static ReadResult Invalid(string reason) => new(false, null, default, reason);
    }

    public static ReadResult TryReadPublicKeyFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return ReadResult.Invalid("the file does not exist");
            }

            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ReadResult.Invalid("the document is not a JSON object");
            }

            if (!TryValidateHeader(root, PublicKeyFormat, out var headerError))
            {
                return ReadResult.Invalid(headerError);
            }

            if (!TryGetBase64(root, "publicKey", out var publicKey, out _))
            {
                return ReadResult.Invalid("the 'publicKey' property is missing or is not base64");
            }

            return new ReadResult(true, publicKey, default, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return ReadResult.Invalid($"the file could not be read ({ex.GetType().Name})");
        }
    }

    public static ReadResult TryReadPrivateKeyFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return ReadResult.Invalid("the file does not exist");
            }

            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ReadResult.Invalid("the document is not a JSON object");
            }

            if (!TryValidateHeader(root, PrivateKeyFormat, out var headerError))
            {
                return ReadResult.Invalid(headerError);
            }

            if (!TryGetBase64(root, "publicKey", out var publicKey, out _))
            {
                return ReadResult.Invalid("the 'publicKey' property is missing or is not base64");
            }

            if (!root.TryGetProperty("parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Object)
            {
                return ReadResult.Invalid("the 'parameters' property is missing or is not an object");
            }

            var rsaParameters = new RSAParameters();
            foreach (var (name, setter) in ParameterSetters)
            {
                if (!TryGetBase64(parameters, name, out _, out var value))
                {
                    return ReadResult.Invalid($"the private key component '{name}' is missing or is not base64");
                }

                setter(ref rsaParameters, value);
            }

            return new ReadResult(true, publicKey, rsaParameters, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return ReadResult.Invalid($"the file could not be read ({ex.GetType().Name})");
        }
    }

    private delegate void ParameterSetter(ref RSAParameters parameters, byte[] value);

    private static readonly (string Name, ParameterSetter Setter)[] ParameterSetters =
    [
        ("modulus", static (ref RSAParameters p, byte[] v) => p.Modulus = v),
        ("exponent", static (ref RSAParameters p, byte[] v) => p.Exponent = v),
        ("d", static (ref RSAParameters p, byte[] v) => p.D = v),
        ("p", static (ref RSAParameters p, byte[] v) => p.P = v),
        ("q", static (ref RSAParameters p, byte[] v) => p.Q = v),
        ("dp", static (ref RSAParameters p, byte[] v) => p.DP = v),
        ("dq", static (ref RSAParameters p, byte[] v) => p.DQ = v),
        ("inverseQ", static (ref RSAParameters p, byte[] v) => p.InverseQ = v),
    ];

    private static bool TryValidateHeader(JsonElement root, string expectedFormat, out string error)
    {
        if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var versionValue))
        {
            error = "the 'version' property is missing or is not a number";
            return false;
        }

        if (versionValue != CurrentVersion)
        {
            error = $"the document version '{versionValue}' is not supported";
            return false;
        }

        if (!root.TryGetProperty("algorithm", out var algorithm) || algorithm.ValueKind != JsonValueKind.String || algorithm.GetString() != Algorithm)
        {
            error = "the 'algorithm' property is missing or does not match the expected algorithm";
            return false;
        }

        if (!root.TryGetProperty("format", out var format) || format.ValueKind != JsonValueKind.String || format.GetString() != expectedFormat)
        {
            error = "the 'format' property is missing or does not match the expected format";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryGetBase64(JsonElement element, string propertyName, out string text, out byte[] value)
    {
        text = null;
        value = null;

        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var candidate = property.GetString();
        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        try
        {
            value = Convert.FromBase64String(candidate);
        }
        catch (FormatException)
        {
            return false;
        }

        text = candidate;
        return true;
    }

    public static byte[] CreatePublicKeyDocument(string publicKey)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            WriteHeader(writer, PublicKeyFormat);
            writer.WriteString("publicKey", publicKey);
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    public static byte[] CreatePrivateKeyDocument(string publicKey, RSAParameters parameters)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            WriteHeader(writer, PrivateKeyFormat);
            writer.WriteString("publicKey", publicKey);
            writer.WriteStartObject("parameters");
            writer.WriteString("modulus", Convert.ToBase64String(parameters.Modulus));
            writer.WriteString("exponent", Convert.ToBase64String(parameters.Exponent));
            writer.WriteString("d", Convert.ToBase64String(parameters.D));
            writer.WriteString("p", Convert.ToBase64String(parameters.P));
            writer.WriteString("q", Convert.ToBase64String(parameters.Q));
            writer.WriteString("dp", Convert.ToBase64String(parameters.DP));
            writer.WriteString("dq", Convert.ToBase64String(parameters.DQ));
            writer.WriteString("inverseQ", Convert.ToBase64String(parameters.InverseQ));
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    private static void WriteHeader(Utf8JsonWriter writer, string format)
    {
        writer.WriteNumber("version", CurrentVersion);
        writer.WriteString("algorithm", Algorithm);
        writer.WriteString("format", format);
    }

    /// <summary>
    /// Exports the base64 X.509 SubjectPublicKeyInfo of <paramref name="rsa"/>. This is the exact
    /// representation <c>crypto.subtle.importKey('spki', ...)</c> expects in the browser.
    /// </summary>
    public static string ExportPublicKey(RSA rsa)
#if NET
        => Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
#else
        => Convert.ToBase64String(SubjectPublicKeyInfoWriter.Write(rsa.ExportParameters(includePrivateParameters: false)));
#endif

    /// <summary>
    /// Derives the base64 SubjectPublicKeyInfo that corresponds to a private key so that a key file
    /// pair can be checked for consistency without trusting the recorded value.
    /// </summary>
    public static bool TryDerivePublicKey(RSAParameters parameters, out string publicKey)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportParameters(parameters);
            publicKey = ExportPublicKey(rsa);
            return true;
        }
        catch (CryptographicException)
        {
            publicKey = null;
            return false;
        }
    }

    public static (string PublicKey, RSAParameters Parameters) Create()
    {
        // The key size has to be requested at creation time: on .NET Framework the provider creates
        // the key eagerly and assigning KeySize afterwards would not regenerate it.
#if NET
        using var rsa = RSA.Create(KeySizeInBits);
#else
        using var rsa = new RSACryptoServiceProvider(KeySizeInBits);
#endif
        return (ExportPublicKey(rsa), rsa.ExportParameters(includePrivateParameters: true));
    }

#if !NET
    /// <summary>
    /// Minimal DER writer for the X.509 SubjectPublicKeyInfo of an RSA public key. .NET Framework
    /// has no <c>ExportSubjectPublicKeyInfo</c>, and the value has to be byte for byte identical to
    /// the one .NET produces because <c>dotnet watch</c> validates the two against each other.
    /// </summary>
    private static class SubjectPublicKeyInfoWriter
    {
        private static readonly byte[] RsaEncryptionOid = [0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01, 0x01];

        public static byte[] Write(RSAParameters parameters)
        {
            if (parameters.Modulus == null || parameters.Exponent == null)
            {
                throw new CryptographicException("The RSA parameters do not contain public key information.");
            }

            // RSAPublicKey ::= SEQUENCE { modulus INTEGER, publicExponent INTEGER }
            using var rsaPublicKey = new MemoryStream();
            WriteInteger(rsaPublicKey, parameters.Modulus);
            WriteInteger(rsaPublicKey, parameters.Exponent);
            var rsaPublicKeySequence = WrapSequence(rsaPublicKey.ToArray());

            // AlgorithmIdentifier ::= SEQUENCE { algorithm OBJECT IDENTIFIER, parameters NULL }
            using var algorithm = new MemoryStream();
            WriteTagged(algorithm, 0x06, RsaEncryptionOid);
            WriteTagged(algorithm, 0x05, []);
            var algorithmSequence = WrapSequence(algorithm.ToArray());

            // BIT STRING with zero unused bits wrapping the RSAPublicKey.
            var bitStringContent = new byte[rsaPublicKeySequence.Length + 1];
            bitStringContent[0] = 0x00;
            Array.Copy(rsaPublicKeySequence, 0, bitStringContent, 1, rsaPublicKeySequence.Length);

            using var subjectPublicKeyInfo = new MemoryStream();
            subjectPublicKeyInfo.Write(algorithmSequence, 0, algorithmSequence.Length);
            WriteTagged(subjectPublicKeyInfo, 0x03, bitStringContent);

            return WrapSequence(subjectPublicKeyInfo.ToArray());
        }

        private static byte[] WrapSequence(byte[] content)
        {
            using var stream = new MemoryStream();
            WriteTagged(stream, 0x30, content);
            return stream.ToArray();
        }

        private static void WriteTagged(Stream stream, byte tag, byte[] content)
        {
            stream.WriteByte(tag);
            WriteLength(stream, content.Length);
            stream.Write(content, 0, content.Length);
        }

        private static void WriteInteger(Stream stream, byte[] value)
        {
            var offset = 0;
            while (offset < value.Length - 1 && value[offset] == 0)
            {
                offset++;
            }

            var length = value.Length - offset;
            var needsPadding = value[offset] > 0x7f;

            stream.WriteByte(0x02);
            WriteLength(stream, needsPadding ? length + 1 : length);
            if (needsPadding)
            {
                stream.WriteByte(0x00);
            }

            stream.Write(value, offset, length);
        }

        private static void WriteLength(Stream stream, int length)
        {
            if (length < 0x80)
            {
                stream.WriteByte((byte)length);
                return;
            }

            var bytesRequired = 0;
            for (var remaining = length; remaining > 0; remaining >>= 8)
            {
                bytesRequired++;
            }

            stream.WriteByte((byte)(bytesRequired | 0x80));
            for (var i = bytesRequired - 1; i >= 0; i--)
            {
                stream.WriteByte((byte)((length >> (8 * i)) & 0xff));
            }
        }
    }
#endif
}
