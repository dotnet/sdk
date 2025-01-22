// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Microsoft.NET.Sdk.StaticWebAssets;

// Represents a manifest of static resources.
// The manifes is a JSON file that contains a list of static resources and their associated metadata.
// There is a top level property "resources" that contains an array of objects, each of which represents a static resource.
// Each static  resource is defined by the following properties:
// * "path": The path of the static resource.
// * "selectors": An array of request headers that act as selectors for the resource. Each selector is defined by an object with the following properties:
//   * "name": The name of the request header.
//   * "value": The value of the request header.
//   * "preference": The preference of the selector. The preference is a number between 0 and 1.0 and it matches the semantics of the quality parameter in
//     the Accept-* headers. This preference is used as a last resource to break ties in content negotiation when the client indicates an equal preference
//     for multiple resources.
// * "responseHeaders": A list of headers to apply to the response when a given resource is served. This is useful to apply headers to the response that are
//   specific to the resource, such as Cache-Control headers or ETag headers that are computed at build time.
internal sealed class StaticAssetsManifest
{
    internal static StaticAssetsManifest Parse(string manifestPath)
    {
        using var stream = File.OpenRead(manifestPath);
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        var result = JsonSerializer.Deserialize(content, StaticAssetsManifestJsonContext.Default.StaticAssetsManifest) ??
            throw new InvalidOperationException($"The static resources manifest file '{manifestPath}' could not be deserialized.");

        return result;
    }

    public int Version { get; set; }

    public string ManifestType { get; set; } = "";

    public List<StaticAssetDescriptor> Endpoints { get; set; } = [];

    public bool IsBuildManifest() => string.Equals(ManifestType, "Build", StringComparison.OrdinalIgnoreCase);
}

[JsonSerializable(typeof(StaticAssetsManifest))]
internal sealed partial class StaticAssetsManifestJsonContext : JsonSerializerContext
{
}

/// <summary>
/// The description of a static asset that was generated during the build process.
/// </summary>
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal sealed class StaticAssetDescriptor
{
    private bool _isFrozen;
    private string? _route;
    private string? _assetFile;
    private IReadOnlyList<StaticAssetSelector> _selectors = [];
    private IReadOnlyList<StaticAssetProperty> _endpointProperties = [];
    private IReadOnlyList<StaticAssetResponseHeader> _responseHeaders = [];

    /// <summary>
    /// The route that the asset is served from.
    /// </summary>
    public string Route
    {
        get => _route ?? throw new InvalidOperationException("Route is required");
        set => _route = !_isFrozen ? value : throw new InvalidOperationException("StaticAssetDescriptor is frozen and doesn't accept further changes");
    }

    /// <summary>
    /// The path to the asset file from the wwwroot folder.
    /// </summary>
    [JsonPropertyName("AssetFile")]
    public string AssetPath
    {
        get => _assetFile ?? throw new InvalidOperationException("AssetPath is required");
        set => _assetFile = !_isFrozen ? value : throw new InvalidOperationException("StaticAssetDescriptor is frozen and doesn't accept further changes");
    }

    /// <summary>
    /// A list of selectors that are used to discriminate between two or more assets with the same route.
    /// </summary>
    [JsonPropertyName("Selectors")]
    public IReadOnlyList<StaticAssetSelector> Selectors
    {
        get => _selectors;
        set => _selectors = !_isFrozen ? value : throw new InvalidOperationException("StaticAssetDescriptor is frozen and doesn't accept further changes");
    }

    /// <summary>
    /// A list of properties that are associated with the endpoint.
    /// </summary>
    [JsonPropertyName("EndpointProperties")]
    public IReadOnlyList<StaticAssetProperty> Properties
    {
        get => _endpointProperties;
        set => _endpointProperties = !_isFrozen ? value : throw new InvalidOperationException("StaticAssetDescriptor is frozen and doesn't accept further changes");
    }

    /// <summary>
    /// A list of headers to apply to the response when this resource is served.
    /// </summary>
    [JsonPropertyName("ResponseHeaders")]
    public IReadOnlyList<StaticAssetResponseHeader> ResponseHeaders
    {
        get => _responseHeaders;
        set => _responseHeaders = !_isFrozen ? value : throw new InvalidOperationException("StaticAssetDescriptor is frozen and doesn't accept further changes");
    }

    private string GetDebuggerDisplay()
    {
        return $"Route: {Route} Path: {AssetPath}";
    }

    internal void Freeze()
    {
        _isFrozen = true;
    }
}

/// <summary>
/// A static asset selector. Selectors are used to discriminate between two or more assets with the same route.
/// </summary>
/// <param name="name">The name associated to the selector.</param>
/// <param name="value">The value associated to the selector and used to match against incoming requests.</param>
/// <param name="quality">The static server quality associated to this selector.</param>
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal sealed class StaticAssetSelector(string name, string value, string quality)
{
    /// <summary>
    /// The name associated to the selector.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// The value associated to the selector and used to match against incoming requests.
    /// </summary>
    public string Value { get; } = value;

    /// <summary>
    /// The static asset server quality associated to this selector. Used to break ties when a request matches multiple values
    /// with the same degree of specificity.
    /// </summary>
    public string Quality { get; } = quality;

    private string GetDebuggerDisplay() => $"Name: {Name} Value: {Value} Quality: {Quality}";
}

/// <summary>
/// A property associated with a static asset.
/// </summary>
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal sealed class StaticAssetProperty(string name, string value)
{
    /// <summary>
    /// The name of the property.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// The value of the property.
    /// </summary>
    public string Value { get; } = value;

    private string GetDebuggerDisplay() => $"Name: {Name} Value:{Value}";
}

/// <summary>
/// A response header to apply to the response when a static asset is served.
/// </summary>
/// <param name="name">The name of the header.</param>
/// <param name="value">The value of the header.</param>
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal sealed class StaticAssetResponseHeader(string name, string value)
{
    /// <summary>
    /// The name of the header.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// The value of the header.
    /// </summary>
    public string Value { get; } = value;

    private string GetDebuggerDisplay() => $"Name: {Name} Value: {Value}";
}
