// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// The build-owned key outputs shared by the browser tools and mobile Hot Reload transports.
///
/// The direction of this channel is deliberate. The browser authenticates the provider with a key
/// the application pins at build time, so the key has to be created by the build; a key that the
/// watcher pushed into the build through a global property would let the provider authenticate
/// itself, and a global property could not be scoped to a project anyway. The build therefore owns
/// the key pair, and the watcher reads it back per project instance.
///
/// The paths are derived from evaluated MSBuild properties, rather than by searching the file system.
/// They must stay in sync with Microsoft.NET.Sdk.StaticWebAssets.HotReload.targets and
/// _GenerateHotReloadWebSocketKey in Microsoft.NET.Sdk.targets.
/// </summary>
internal sealed class BrowserToolsBuildOutputs
{
    /// <summary>
    /// Subdirectory of the project's intermediate output that holds the browser tools build outputs.
    /// </summary>
    public const string DirectoryName = "HotReload";

    public const string PublicKeyFileName = "browser-tools-key.public.json";
    public const string PrivateKeyFileName = "browser-tools-key.private.json";

    private const int SupportedKeyDocumentVersion = 1;
    private const string SupportedKeyAlgorithm = "RSA-OAEP-SHA256";
    private const string PublicKeyFormat = "SubjectPublicKeyInfo";
    private const string PrivateKeyFormat = "RSAParameters";

    private readonly string _publicKeyPath;
    private readonly string _privateKeyPath;

    public BrowserToolsBuildOutputs(string directory, string publicKeyFileName, string privateKeyFileName)
    {
        _publicKeyPath = Path.Combine(directory, publicKeyFileName);
        _privateKeyPath = Path.Combine(directory, privateKeyFileName);
    }

    /// <summary>
    /// Returns the build outputs of the given project, or null when the project does
    /// not produce browser tools assets at all.
    ///
    /// Whether it does is decided by evaluated properties that mirror the condition on the target that produces them,
    /// so a project that opted out - or an SDK that has no browser tools initializer - is recognized without touching the file
    /// system.
    /// </summary>
    public static BrowserToolsBuildOutputs? FromProjectSettings(
        string configuration,
        string? intermediateOutputDirectory,
        bool? enableHotReloadInRuntimeConfigDevFile,
        string browserToolsAssetPrefix,
        bool staticWebAssetsEnabled,
        bool jsModulesEnabled)
    {
        var enableHotReload = enableHotReloadInRuntimeConfigDevFile ?? string.Equals(configuration, "Debug", StringComparison.OrdinalIgnoreCase);

        if (!enableHotReload ||
            browserToolsAssetPrefix is not { Length: > 0 } ||
            !staticWebAssetsEnabled ||
            !jsModulesEnabled ||
            intermediateOutputDirectory is null)
        {
            return null;
        }

        return new BrowserToolsBuildOutputs(Path.Combine(intermediateOutputDirectory, DirectoryName), PublicKeyFileName, PrivateKeyFileName);
    }

    /// <summary>
    /// Creates the outputs for a directory directly, bypassing project evaluation. Only used by
    /// tests: production code always derives the directory from the project instance so that the
    /// paths cannot drift from what the build wrote.
    /// </summary>
    internal static BrowserToolsBuildOutputs CreateForTesting(string directory)
        => new(directory, PublicKeyFileName, PrivateKeyFileName);

    /// <summary>
    /// Creates the provider's session key from the private half the build produced, after checking
    /// that it is the key the application pinned. Never logs key material.
    /// </summary>
    public SharedSecretProvider? TryCreateSessionKey(ILogger logger)
    {
        try
        {
            var pinnedPublicKey = ReadPinnedPublicKey();
            var parameters = ReadPrivateKey(out var recordedPublicKey);

            if (!string.Equals(pinnedPublicKey, recordedPublicKey, StringComparison.Ordinal))
            {
                throw Fail("the public and private key documents describe different keys");
            }

            SharedSecretProvider sessionKey;
            try
            {
                sessionKey = new SharedSecretProvider(parameters);
            }
            catch (CryptographicException)
            {
                throw Fail("the private key could not be imported");
            }

            // The recorded value is not trusted on its own: the key that the provider will actually use
            // has to be the key the application pinned, otherwise the browser rejects the provider.
            if (!string.Equals(sessionKey.GetPublicKey(), pinnedPublicKey, StringComparison.Ordinal))
            {
                sessionKey.Dispose();
                throw Fail("the private key does not match the public key the application pinned");
            }

            logger.Log(LogEvents.BrowserToolsUsingKeyFromBuild);
            return sessionKey;
        }
        catch (Exception e)
        {
            logger.LogError("Unable to load the browser tools session key. {Message}", e.Message);
            return null;
        }
    }

    private string ReadPinnedPublicKey()
    {
        using var document = ReadDocument(_publicKeyPath);
        ValidateHeader(document.RootElement, PublicKeyFormat, _publicKeyPath);
        return ReadRequiredBase64(document.RootElement, "publicKey", _publicKeyPath);
    }

    private RSAParameters ReadPrivateKey(out string recordedPublicKey)
    {
        using var document = ReadDocument(_privateKeyPath);
        var root = document.RootElement;
        ValidateHeader(root, PrivateKeyFormat, _privateKeyPath);
        recordedPublicKey = ReadRequiredBase64(root, "publicKey", _privateKeyPath);

        if (!root.TryGetProperty("parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Object)
        {
            throw Fail($"'{Path.GetFileName(_privateKeyPath)}' does not contain the private key components");
        }

        return new RSAParameters
        {
            Modulus = ReadRequiredBase64Bytes(parameters, "modulus", _privateKeyPath),
            Exponent = ReadRequiredBase64Bytes(parameters, "exponent", _privateKeyPath),
            D = ReadRequiredBase64Bytes(parameters, "d", _privateKeyPath),
            P = ReadRequiredBase64Bytes(parameters, "p", _privateKeyPath),
            Q = ReadRequiredBase64Bytes(parameters, "q", _privateKeyPath),
            DP = ReadRequiredBase64Bytes(parameters, "dp", _privateKeyPath),
            DQ = ReadRequiredBase64Bytes(parameters, "dq", _privateKeyPath),
            InverseQ = ReadRequiredBase64Bytes(parameters, "inverseQ", _privateKeyPath),
        };
    }

    private JsonDocument ReadDocument(string path)
    {
        byte[] content;
        try
        {
            content = File.ReadAllBytes(path);
        }
        catch (FileNotFoundException)
        {
            throw Fail($"'{Path.GetFileName(path)}' was not produced by the build");
        }
        catch (DirectoryNotFoundException)
        {
            throw Fail($"'{Path.GetFileName(path)}' was not produced by the build");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw Fail($"'{Path.GetFileName(path)}' could not be read ({e.GetType().Name})");
        }

        try
        {
            return JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            throw Fail($"'{Path.GetFileName(path)}' is not a valid JSON document");
        }
    }

    private void ValidateHeader(JsonElement root, string expectedFormat, string path)
    {
        var name = Path.GetFileName(path);

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw Fail($"'{name}' is not a JSON object");
        }

        if (!root.TryGetProperty("version", out var version) ||
            version.ValueKind != JsonValueKind.Number ||
            !version.TryGetInt32(out var versionValue) ||
            versionValue != SupportedKeyDocumentVersion)
        {
            throw Fail($"'{name}' does not have the supported document version {SupportedKeyDocumentVersion}");
        }

        if (!root.TryGetProperty("algorithm", out var algorithm) ||
            algorithm.ValueKind != JsonValueKind.String ||
            algorithm.GetString() != SupportedKeyAlgorithm)
        {
            throw Fail($"'{name}' does not use the supported key algorithm");
        }

        if (!root.TryGetProperty("format", out var format) ||
            format.ValueKind != JsonValueKind.String ||
            format.GetString() != expectedFormat)
        {
            throw Fail($"'{name}' does not use the expected key format '{expectedFormat}'");
        }
    }

    private string ReadRequiredBase64(JsonElement element, string propertyName, string path)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            property.GetString() is not { Length: > 0 } value)
        {
            throw Fail($"'{Path.GetFileName(path)}' does not contain '{propertyName}'");
        }

        try
        {
            _ = Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            throw Fail($"'{propertyName}' in '{Path.GetFileName(path)}' is not base64");
        }

        return value;
    }

    private byte[] ReadRequiredBase64Bytes(JsonElement element, string propertyName, string path)
        => Convert.FromBase64String(ReadRequiredBase64(element, propertyName, path));

    private Exception Fail(string reason)
        => new($"Unable to start the HotReload browser tools because {reason}. Rebuild the project to regenerate the browser tools build outputs.");
}
