// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;

namespace Microsoft.DotNet.HotReload.UnitTests;

[TestClass]
public class SharedSecretProviderTests
{
    private static byte[] GetRandomBytes(int length)
    {
        var result = new byte[length];
        var random = new Random();
        random.NextBytes(result);
        return result;
    }

    [TestMethod]
    public void EncryptDecrypt()
    {
        using var provider = new SharedSecretProvider();

        // Server generates public key and sends it over to the middleware:
        var publicKeyNetfx = provider.ExportPublicKeyNetFramework();
        var publicKeyParameters = provider.ExportPublicKeyParameters();
        var publicKey = provider.GetPublicKey();

        // Middleware embeds key by in the .js file loaded to the browser:
        Assert.AreEqual(publicKey, publicKeyNetfx);

        // The browser generates 32-byte random secret:
        var secret = GetRandomBytes(32);
        var secretBase64 = Convert.ToBase64String(secret);

        // The secret is encrypted using public key and sent
        // as subprotocol when client connects to the server over WebSocket:
        var encrypted = CryptoExtensions.GetEncryptedSecret(publicKey, publicKeyParameters, secret);

        // The server decrypts the secret using private key.
        // The secret is sent over to the client with every request over WebSocket.
        // The client validates that the secrete matches the one it generated.
        var decrypted = provider.DecryptSecret(encrypted);
        Assert.AreEqual(secretBase64, decrypted);
    }
}
