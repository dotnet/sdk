// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.Watch.UnitTests;

/// <summary>
/// The build owns the browser tools key pair; dotnet-watch only reads it back from deterministic
/// paths under the project's intermediate output. These tests pin the two halves of that contract:
/// the provider is keyed with the key the application pinned, and
/// anything that would make the provider unusable fails loudly instead of silently disabling the
/// browser tools, because a browser that pinned a key can never be told that the tools are off.
/// </summary>
[TestClass]
public class BrowserToolsBuildOutputsTests : IDisposable
{
    private readonly string _directory;
    private readonly string _publicKeyPath;
    private readonly string _privateKeyPath;

    public BrowserToolsBuildOutputsTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "dotnet-watch-browser-tools", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _publicKeyPath = Path.Combine(_directory, BrowserToolsBuildOutputs.PublicKeyFileName);
        _privateKeyPath = Path.Combine(_directory, BrowserToolsBuildOutputs.PrivateKeyFileName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private BrowserToolsBuildOutputs CreateOutputs()
        => BrowserToolsBuildOutputs.CreateForTesting("test.csproj", _directory, NullLogger.Instance);

    [TestMethod]
    public void EnableHotReload_ChangesOnlyDisabledSettings()
    {
        var outputs = CreateOutputs();
        File.WriteAllText(outputs.SettingsPath, "{ \"hotReload\": false }" + Environment.NewLine);
        File.SetLastWriteTimeUtc(outputs.SettingsPath, DateTime.UtcNow.AddMinutes(-2));
        var before = File.GetLastWriteTimeUtc(outputs.SettingsPath);

        outputs.EnableHotReload();
        Assert.AreEqual("{ \"hotReload\": true }" + Environment.NewLine, File.ReadAllText(outputs.SettingsPath));

        var enabled = File.GetLastWriteTimeUtc(outputs.SettingsPath);
        outputs.EnableHotReload();
        Assert.AreEqual(enabled, File.GetLastWriteTimeUtc(outputs.SettingsPath));
        Assert.AreNotEqual(before, File.GetLastWriteTimeUtc(outputs.SettingsPath));

        outputs.DisableHotReload();
        Assert.AreEqual("{ \"hotReload\": false }" + Environment.NewLine, File.ReadAllText(outputs.SettingsPath));
        var disabled = File.GetLastWriteTimeUtc(outputs.SettingsPath);
        outputs.DisableHotReload();
        Assert.AreEqual(disabled, File.GetLastWriteTimeUtc(outputs.SettingsPath));
    }

    [TestMethod]
    public void EnableHotReload_RequiresTheBuildSettingsFile()
    {
        var outputs = CreateOutputs();
        Assert.ThrowsExactly<BrowserToolsBuildOutputsException>(outputs.EnableHotReload);
    }

    private RSA WriteValidKeyPair()
    {
        var rsa = RSA.Create(2048);
        var publicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        var p = rsa.ExportParameters(includePrivateParameters: true);

        File.WriteAllText(_publicKeyPath, $$"""
            {
              "version": 1,
              "algorithm": "RSA-OAEP-SHA256",
              "format": "SubjectPublicKeyInfo",
              "publicKey": "{{publicKey}}"
            }
            """);

        File.WriteAllText(_privateKeyPath, $$"""
            {
              "version": 1,
              "algorithm": "RSA-OAEP-SHA256",
              "format": "RSAParameters",
              "publicKey": "{{publicKey}}",
              "parameters": {
                "modulus": "{{Convert.ToBase64String(p.Modulus!)}}",
                "exponent": "{{Convert.ToBase64String(p.Exponent!)}}",
                "d": "{{Convert.ToBase64String(p.D!)}}",
                "p": "{{Convert.ToBase64String(p.P!)}}",
                "q": "{{Convert.ToBase64String(p.Q!)}}",
                "dp": "{{Convert.ToBase64String(p.DP!)}}",
                "dq": "{{Convert.ToBase64String(p.DQ!)}}",
                "inverseQ": "{{Convert.ToBase64String(p.InverseQ!)}}"
              }
            }
            """);

        return rsa;
    }

    /// <summary>
    /// The provider has to end up holding the key the application pinned, otherwise the browser
    /// rejects it.
    /// </summary>
    [TestMethod]
    public void CreateSessionKey_UsesTheKeyPairTheBuildProduced()
    {
        using var rsa = WriteValidKeyPair();
        var expectedPublicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());

        using var sessionKey = CreateOutputs().CreateSessionKey();

        Assert.AreEqual(expectedPublicKey, sessionKey.GetPublicKey());

        // The provider decrypts the secret the browser encrypts with the pinned public key.
        var secret = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
        var encrypted = Convert.ToBase64String(rsa.Encrypt(Convert.FromBase64String(secret), RSAEncryptionPadding.OaepSHA256));
        Assert.AreEqual(secret, sessionKey.DecryptSecret(encrypted));
    }

    [TestMethod]
    [DataRow(true, false, DisplayName = "Public key document missing")]
    [DataRow(false, true, DisplayName = "Private key document missing")]
    [DataRow(true, true, DisplayName = "Both missing, as after a clean")]
    public void CreateSessionKey_FailsWhenAHalfIsMissing(bool deletePublic, bool deletePrivate)
    {
        WriteValidKeyPair().Dispose();

        if (deletePublic)
        {
            File.Delete(_publicKeyPath);
        }

        if (deletePrivate)
        {
            File.Delete(_privateKeyPath);
        }

        var outputs = CreateOutputs();
        Assert.ThrowsExactly<BrowserToolsBuildOutputsException>(() => outputs.CreateSessionKey());
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("{}")]
    [DataRow("[]")]
    [DataRow("{ \"version\": 2, \"algorithm\": \"RSA-OAEP-SHA256\", \"format\": \"RSAParameters\" }")]
    [DataRow("{ \"version\": 1, \"algorithm\": \"RSA-OAEP-SHA1\", \"format\": \"RSAParameters\" }")]
    public void CreateSessionKey_FailsWhenThePrivateDocumentIsMalformed(string content)
    {
        WriteValidKeyPair().Dispose();
        File.WriteAllText(_privateKeyPath, content);

        var outputs = CreateOutputs();
        Assert.ThrowsExactly<BrowserToolsBuildOutputsException>(() => outputs.CreateSessionKey());
    }

    /// <summary>
    /// Two halves that describe different keys would produce a provider the browser refuses to
    /// authenticate, which has to fail the launch rather than start an application whose browser
    /// tools silently do nothing.
    /// </summary>
    [TestMethod]
    public void CreateSessionKey_FailsWhenTheHalvesDoNotMatch()
    {
        WriteValidKeyPair().Dispose();
        var mismatchedPrivate = File.ReadAllText(_privateKeyPath);

        WriteValidKeyPair().Dispose();
        File.WriteAllText(_privateKeyPath, mismatchedPrivate);

        var outputs = CreateOutputs();
        Assert.ThrowsExactly<BrowserToolsBuildOutputsException>(() => outputs.CreateSessionKey());
    }

    /// <summary>
    /// The recorded public key is not trusted on its own: only the key the provider will actually
    /// use may be compared against the one the application pinned.
    /// </summary>
    [TestMethod]
    public void CreateSessionKey_FailsWhenThePrivateComponentsDescribeADifferentKey()
    {
        using var pinned = WriteValidKeyPair();
        var pinnedPublicKey = Convert.ToBase64String(pinned.ExportSubjectPublicKeyInfo());

        using var other = RSA.Create(2048);
        var p = other.ExportParameters(includePrivateParameters: true);

        File.WriteAllText(_privateKeyPath, $$"""
            {
              "version": 1,
              "algorithm": "RSA-OAEP-SHA256",
              "format": "RSAParameters",
              "publicKey": "{{pinnedPublicKey}}",
              "parameters": {
                "modulus": "{{Convert.ToBase64String(p.Modulus!)}}",
                "exponent": "{{Convert.ToBase64String(p.Exponent!)}}",
                "d": "{{Convert.ToBase64String(p.D!)}}",
                "p": "{{Convert.ToBase64String(p.P!)}}",
                "q": "{{Convert.ToBase64String(p.Q!)}}",
                "dp": "{{Convert.ToBase64String(p.DP!)}}",
                "dq": "{{Convert.ToBase64String(p.DQ!)}}",
                "inverseQ": "{{Convert.ToBase64String(p.InverseQ!)}}"
              }
            }
            """);

        var outputs = CreateOutputs();
        Assert.ThrowsExactly<BrowserToolsBuildOutputsException>(() => outputs.CreateSessionKey());
    }

}
