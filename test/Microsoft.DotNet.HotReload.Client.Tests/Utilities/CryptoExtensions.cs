// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Security.Cryptography;

internal static class CryptoExtensions
{
#if NETFRAMEWORK
    extension(RandomNumberGenerator)
    {
        public static byte[] GetBytes(int count)
        {
            var bytes = new byte[count];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return bytes;
        }
    }
#endif

    /// <summary>
    /// Encrypts a browser generated secret the way the client does, using the public half of the
    /// supplied key. The result is the raw base64 ciphertext; callers that put it on the wire have to
    /// escape it first because a WebSocket subprotocol is an HTTP token.
    /// </summary>
    public static string GetEncryptedSecret(string key, RSAParameters publicKeyParameters, byte[] secret)
    {
        // Import server key for RSA-OAEP
#if NET
        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(key), out _);
#else
        using var rsa = new RSACng();
        rsa.ImportParameters(publicKeyParameters);
#endif
        // Encrypt using RSA-OAEP
        return Convert.ToBase64String(rsa.Encrypt(secret, RSAEncryptionPadding.OaepSHA256));
    }
}
