// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.Watch.UnitTests;

/// <summary>
/// The build owns the browser tools key pair and the settings document; dotnet-watch only reads
/// them back from deterministic paths under the project's intermediate output. These tests pin the
/// two halves of that contract: the provider is keyed with the key the application pinned, and
/// anything that would make the provider unusable fails loudly instead of silently disabling the
/// browser tools, because a browser that pinned a key can never be told that the tools are off.
/// </summary>
[TestClass]
public class BrowserToolsBuildOutputsTests : IDisposable
{
    private readonly string _directory;
    private readonly string _settingsPath;
    private readonly string _publicKeyPath;
    private readonly string _privateKeyPath;

    public BrowserToolsBuildOutputsTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "dotnet-watch-browser-tools", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _settingsPath = Path.Combine(_directory, BrowserToolsBuildOutputs.SettingsFileName);
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

    /// <summary>
    /// The settings document is the only thing that activates the browser tools in the application,
    /// and every build resets it, so dotnet-watch has to be able to flip it back before each launch.
    /// </summary>
    [TestMethod]
    public void EnableHotReload_WritesTheEnabledDocument()
    {
        File.WriteAllText(_settingsPath, "{ \"hotReload\": false }" + Environment.NewLine);

        CreateOutputs().EnableHotReload();

        using var document = JsonDocument.Parse(File.ReadAllBytes(_settingsPath));
        Assert.AreEqual(JsonValueKind.True, document.RootElement.GetProperty("hotReload").ValueKind);

        // Only the boolean: the public key and the routes live in the initializer and configuration.
        Assert.HasCount(1, document.RootElement.EnumerateObject());
    }

    [TestMethod]
    public void EnableHotReload_CreatesTheDocumentWhenItIsMissing()
    {
        CreateOutputs().EnableHotReload();

        using var document = JsonDocument.Parse(File.ReadAllBytes(_settingsPath));
        Assert.AreEqual(JsonValueKind.True, document.RootElement.GetProperty("hotReload").ValueKind);
    }

    /// <summary>
    /// A relaunch that rewrote an already enabled document would touch a file the build tracks and
    /// keep invalidating incremental state and the file watcher for no reason.
    /// </summary>
    [TestMethod]
    public void EnableHotReload_IsANoOpWhenAlreadyEnabled()
    {
        var outputs = CreateOutputs();
        outputs.EnableHotReload();

        var timestamp = File.GetLastWriteTimeUtc(_settingsPath);
        File.SetLastWriteTimeUtc(_settingsPath, timestamp.AddDays(-1));
        var movedTimestamp = File.GetLastWriteTimeUtc(_settingsPath);

        outputs.EnableHotReload();

        Assert.AreEqual(movedTimestamp, File.GetLastWriteTimeUtc(_settingsPath));
    }

    /// <summary>
    /// The build writes the disabled document with WriteLinesToFile. Both sides have to produce the
    /// same bytes so that the "write only when different" checks on either side behave.
    /// </summary>
    [TestMethod]
    public void SettingsDocumentsRoundTripBetweenBothSides()
    {
        var outputs = CreateOutputs();

        outputs.EnableHotReload();
        var enabled = File.ReadAllText(_settingsPath);

        outputs.DisableHotReload();
        var disabled = File.ReadAllText(_settingsPath);

        Assert.AreEqual("{ \"hotReload\": true } " + Environment.NewLine, enabled);
        Assert.AreEqual("{ \"hotReload\": false }" + Environment.NewLine, disabled);
        Assert.AreEqual(enabled.Length, disabled.Length);

        // No BOM: WriteLinesToFile writes UTF-8 without one.
        Assert.AreNotEqual(0xEF, File.ReadAllBytes(_settingsPath)[0]);
    }
}
