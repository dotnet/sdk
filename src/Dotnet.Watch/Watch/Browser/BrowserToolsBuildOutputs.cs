// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

/// <summary>
/// Thrown when a project produced browser tools build outputs that <c>dotnet watch</c> cannot use.
/// The launch fails instead of silently continuing without browser tools, because a provider whose
/// key the application does not pin can never be authenticated by the browser and every browser
/// tools feature would appear to be broken for no visible reason.
/// </summary>
internal sealed class BrowserToolsBuildOutputsException(string message) : Exception(message);

/// <summary>
/// The build outputs the Static Web Assets SDK produces for the <c>dotnet watch</c> browser tools,
/// and the only channel between the build and <c>dotnet watch</c> for them.
///
/// The direction of this channel is deliberate. The browser authenticates the provider with a key
/// the application pins at build time, so the key has to be created by the build; a key that the
/// watcher pushed into the build through a global property would let the provider authenticate
/// itself, and a global property could not be scoped to a project anyway. The build therefore owns
/// the key pair, and the watcher reads it back per project instance.
///
/// The paths are derived from evaluated MSBuild properties, the same way the static web assets
/// manifest is located in <see cref="EvaluationResult"/>, rather than by searching the file system.
/// They must stay in sync with Microsoft.NET.Sdk.StaticWebAssets.DotNetWatch.targets.
/// </summary>
internal sealed class BrowserToolsBuildOutputs
{
    /// <summary>
    /// Subdirectory of the project's intermediate output that holds the browser tools build outputs.
    /// </summary>
    public const string DirectoryName = "dotnet-watch";

    public const string PublicKeyFileName = "browser-tools-key.public.json";
    public const string PrivateKeyFileName = "browser-tools-key.private.json";
    public const string SettingsFileName = "hot-reload-settings.json";
    private static readonly byte[] s_enabledSettings = Encoding.UTF8.GetBytes("{ \"hotReload\": true }" + Environment.NewLine);
    private static readonly byte[] s_disabledSettings = Encoding.UTF8.GetBytes("{ \"hotReload\": false }" + Environment.NewLine);

    private const int SupportedKeyDocumentVersion = 1;
    private const string SupportedKeyAlgorithm = "RSA-OAEP-SHA256";
    private const string PublicKeyFormat = "SubjectPublicKeyInfo";
    private const string PrivateKeyFormat = "RSAParameters";

    private readonly ILogger _logger;

    public string ProjectPath { get; }
    public string PublicKeyPath { get; }
    public string PrivateKeyPath { get; }
    public string SettingsPath { get; }

    private BrowserToolsBuildOutputs(string projectPath, string directory, ILogger logger)
    {
        _logger = logger;
        ProjectPath = projectPath;
        PublicKeyPath = Path.Combine(directory, PublicKeyFileName);
        PrivateKeyPath = Path.Combine(directory, PrivateKeyFileName);
        SettingsPath = Path.Combine(directory, SettingsFileName);
    }

    public void EnableHotReload()
        => WriteSettings(s_enabledSettings);

    public void DisableHotReload()
        => WriteSettings(s_disabledSettings);

    private void WriteSettings(byte[] settings)
    {
        byte[] current;
        try
        {
            current = File.ReadAllBytes(SettingsPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw Fail($"'{SettingsFileName}' could not be read ({e.GetType().Name})");
        }

        if (!current.AsSpan().SequenceEqual(settings))
        {
            try
            {
                File.WriteAllBytes(SettingsPath, settings);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw Fail($"'{SettingsFileName}' could not be updated ({e.GetType().Name})");
            }
        }
    }

    /// <summary>
    /// Returns the build outputs of <paramref name="projectNode"/>, or null when the project does
    /// not produce browser tools assets at all. Whether it does is decided by evaluated properties
    /// that mirror the condition on the target that produces them, so a project that opted out - or
    /// an SDK that has no browser tools initializer - is recognized without touching the file
    /// system.
    /// </summary>
    public static BrowserToolsBuildOutputs? TryGetFor(ProjectGraphNode projectNode, ILogger logger)
        => TryGetFor(projectNode.ProjectInstance, logger);

    public static BrowserToolsBuildOutputs? TryGetFor(ProjectInstance projectInstance, ILogger logger)
    {
        var enableHotReload = projectInstance.GetBooleanPropertyValue(
            PropertyNames.EnableHotReloadInRuntimeConfigDevFile,
            defaultValue: string.Equals(
                projectInstance.GetPropertyValue(PropertyNames.Configuration),
                "Debug",
                StringComparison.OrdinalIgnoreCase));

        if (!enableHotReload ||
            projectInstance.GetPropertyValue(PropertyNames.DotNetWatchBrowserToolsAssetPrefix) is not { Length: > 0 } ||
            !projectInstance.GetBooleanPropertyValue(PropertyNames.StaticWebAssetsEnabled) ||
            !projectInstance.GetBooleanPropertyValue(PropertyNames.JSModulesEnabled))
        {
            logger.Log(MessageDescriptor.BrowserToolsAssetsNotProducedByProject);
            return null;
        }

        if (projectInstance.GetIntermediateOutputDirectory() is not { } intermediateOutputDirectory)
        {
            logger.Log(MessageDescriptor.BrowserToolsAssetsNotProducedByProject);
            return null;
        }

        return new BrowserToolsBuildOutputs(
            projectInstance.FullPath,
            Path.Combine(intermediateOutputDirectory, DirectoryName),
            logger);
    }

    /// <summary>
    /// Creates the outputs for a directory directly, bypassing project evaluation. Only used by
    /// tests: production code always derives the directory from the project instance so that the
    /// paths cannot drift from what the build wrote.
    /// </summary>
    internal static BrowserToolsBuildOutputs CreateForTesting(string projectPath, string directory, ILogger logger)
        => new(projectPath, directory, logger);

    /// <summary>
    /// Creates the provider's session key from the private half the build produced, after checking
    /// that it is the key the application pinned. Never logs key material.
    /// </summary>
    /// <exception cref="BrowserToolsBuildOutputsException">
    /// The key documents are missing, malformed, or describe different keys.
    /// </exception>
    public SharedSecretProvider CreateSessionKey()
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

        _logger.Log(MessageDescriptor.BrowserToolsUsingKeyFromBuild);
        return sessionKey;
    }

    private string ReadPinnedPublicKey()
    {
        using var document = ReadDocument(PublicKeyPath);
        ValidateHeader(document.RootElement, PublicKeyFormat, PublicKeyPath);
        return ReadRequiredBase64(document.RootElement, "publicKey", PublicKeyPath);
    }

    private RSAParameters ReadPrivateKey(out string recordedPublicKey)
    {
        using var document = ReadDocument(PrivateKeyPath);
        var root = document.RootElement;
        ValidateHeader(root, PrivateKeyFormat, PrivateKeyPath);
        recordedPublicKey = ReadRequiredBase64(root, "publicKey", PrivateKeyPath);

        if (!root.TryGetProperty("parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Object)
        {
            throw Fail($"'{Path.GetFileName(PrivateKeyPath)}' does not contain the private key components");
        }

        return new RSAParameters
        {
            Modulus = ReadRequiredBase64Bytes(parameters, "modulus", PrivateKeyPath),
            Exponent = ReadRequiredBase64Bytes(parameters, "exponent", PrivateKeyPath),
            D = ReadRequiredBase64Bytes(parameters, "d", PrivateKeyPath),
            P = ReadRequiredBase64Bytes(parameters, "p", PrivateKeyPath),
            Q = ReadRequiredBase64Bytes(parameters, "q", PrivateKeyPath),
            DP = ReadRequiredBase64Bytes(parameters, "dp", PrivateKeyPath),
            DQ = ReadRequiredBase64Bytes(parameters, "dq", PrivateKeyPath),
            InverseQ = ReadRequiredBase64Bytes(parameters, "inverseQ", PrivateKeyPath),
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

    private BrowserToolsBuildOutputsException Fail(string reason)
        => new($"Unable to start the dotnet-watch browser tools for '{ProjectPath}' because {reason}. Rebuild the project to regenerate the browser tools build outputs.");
}
