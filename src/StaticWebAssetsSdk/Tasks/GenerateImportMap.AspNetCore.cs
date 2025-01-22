// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using System.Collections;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Microsoft.NET.Sdk.StaticWebAssets;

/// <summary>
/// Represents the contents of a <c><script type="importmap"></script></c> element that defines the import map
/// for module scripts in the application.
/// </summary>
/// <remarks>
/// The import map is a JSON object that defines the mapping of module import specifiers to URLs.
/// <see cref="ImportMapDefinition"/> instances are expensive to create, so it is recommended to cache them if
/// you are creating an additional instance.
/// </remarks>
public sealed class ImportMapDefinition
{
    private Dictionary<string, string>? _imports;
    private Dictionary<string, IReadOnlyDictionary<string, string>>? _scopes;
    private Dictionary<string, string>? _integrity;
    private string? _json;

    /// <summary>
    /// Initializes a new instance of <see cref="ImportMapDefinition"/>."/> with the specified imports, scopes, and integrity.
    /// </summary>
    /// <param name="imports">The unscoped imports defined in the import map.</param>
    /// <param name="scopes">The scoped imports defined in the import map.</param>
    /// <param name="integrity">The integrity for the imports defined in the import map.</param>
    /// <remarks>
    /// The <paramref name="imports"/>, <paramref name="scopes"/>, and <paramref name="integrity"/> parameters
    /// will be copied into the new instance. The original collections will not be modified.
    /// </remarks>
    public ImportMapDefinition(
        IReadOnlyDictionary<string, string>? imports,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? scopes,
        IReadOnlyDictionary<string, string>? integrity)
    {
        _imports = imports?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        _integrity = integrity?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        _scopes = scopes?.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToDictionary(scopeKvp => scopeKvp.Key, scopeKvp => scopeKvp.Value) as IReadOnlyDictionary<string, string>);
    }

    private ImportMapDefinition()
    {
    }

    /// <summary>
    /// Creates an import map from a <see cref="ResourceAssetCollection"/>.
    /// </summary>
    /// <param name="assets">The collection of assets to create the import map from.</param>
    /// <returns>The import map.</returns>
    public static ImportMapDefinition FromResourceCollection(ResourceAssetCollection assets)
    {
        var importMap = new ImportMapDefinition();
        foreach (var asset in assets)
        {
            if (!(asset.Url.EndsWith(".mjs", StringComparison.OrdinalIgnoreCase) ||
                asset.Url.EndsWith(".js", StringComparison.OrdinalIgnoreCase)) ||
                asset.Properties == null)
            {
                continue;
            }

            var (integrity, label) = GetAssetProperties(asset);
            if (integrity != null)
            {
                importMap._integrity ??= [];
                importMap._integrity[$"./{asset.Url}"] = integrity;
            }

            if (label != null)
            {
                importMap._imports ??= [];
                importMap._imports[$"./{label}"] = $"./{asset.Url}";
            }
        }

        return importMap;
    }

    private static (string? integrity, string? label) GetAssetProperties(ResourceAsset asset)
    {
        string? integrity = null;
        string? label = null;
        for (var i = 0; i < asset.Properties!.Count; i++)
        {
            var property = asset.Properties[i];
            if (string.Equals(property.Name, "integrity", StringComparison.OrdinalIgnoreCase))
            {
                integrity = property.Value;
            }
            else if (string.Equals(property.Name, "label", StringComparison.OrdinalIgnoreCase))
            {
                label = property.Value;
            }

            if (integrity != null && label != null)
            {
                return (integrity, label);
            }
        }

        return (integrity, label);
    }

    /// <summary>
    /// Combines one or more import maps into a single import map.
    /// </summary>
    /// <param name="sources">The list of import maps to combine.</param>
    /// <returns>
    /// A new import map that is the combination of all the input import maps with their
    /// entries applied in order.
    /// </returns>
    public static ImportMapDefinition Combine(params ImportMapDefinition[] sources)
    {
        var importMap = new ImportMapDefinition();
        foreach (var item in sources)
        {
            if (item.Imports != null)
            {
                importMap._imports ??= [];
                foreach (var keyValue in item.Imports)
                {
                    importMap._imports[keyValue.Key] = keyValue.Value;
                }
            }

            if (item.Scopes != null)
            {
                importMap._scopes ??= [];
                foreach (var keyValue in item.Scopes)
                {
                    if (importMap._scopes.TryGetValue(keyValue.Key, out var existingScope) && existingScope != null)
                    {
                        foreach (var scopeKeyValue in keyValue.Value)
                        {
                            ((Dictionary<string, string>)importMap._scopes[keyValue.Key])[scopeKeyValue.Key] = scopeKeyValue.Value;
                        }
                    }
                    else
                    {
                        importMap._scopes[keyValue.Key] = keyValue.Value;
                    }
                }
            }

            if (item.Integrity != null)
            {
                importMap._integrity ??= [];
                foreach (var keyValue in item.Integrity)
                {
                    importMap._integrity[keyValue.Key] = keyValue.Value;
                }
            }
        }

        return importMap;
    }

    // Example:
    // "imports": {
    //   "triangle": "./module/shapes/triangle.js",
    //   "pentagram": "https://example.com/shapes/pentagram.js"
    // }
    /// <summary>
    /// Gets the unscoped imports defined in the import map.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Imports { get => _imports; }

    // Example:
    // {
    //   "imports": {
    //     "triangle": "./module/shapes/triangle.js"
    //   },
    //   "scopes": {
    //     "/modules/myshapes/": {
    //       "triangle": "https://example.com/modules/myshapes/triangle.js"
    //     }
    //   }
    // }
    /// <summary>
    /// Gets the scoped imports defined in the import map.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? Scopes { get => _scopes; }

    // Example:
    // <script type="importmap">
    // {
    //   "imports": {
    //     "triangle": "./module/shapes/triangle.js"
    //   },
    //   "integrity": {
    //     "./module/shapes/triangle.js": "sha256-..."
    //   }
    // }
    // </script>
    /// <summary>
    /// Gets the integrity properties defined in the import map.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Integrity { get => _integrity; }

    internal string ToJson()
    {
        _json ??= JsonSerializer.Serialize(this, ImportMapSerializerContext.CustomEncoder.Options);
        return _json;
    }

    /// <inheritdoc />
    public override string ToString() => ToJson();
}

[JsonSerializable(typeof(ImportMapDefinition))]
internal sealed partial class ImportMapSerializerContext : JsonSerializerContext
{
    private static ImportMapSerializerContext? _customEncoder;

    public static ImportMapSerializerContext CustomEncoder => _customEncoder ??= new(new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    });
}

/// <summary>
/// Describes a mapping of static assets to their corresponding unique URLs.
/// </summary>
public sealed class ResourceAssetCollection : IReadOnlyList<ResourceAsset>
{
    /// <summary>
    /// An empty <see cref="ResourceAssetCollection"/>.
    /// </summary>
    public static readonly ResourceAssetCollection Empty = new([]);

    private readonly FrozenDictionary<string, ResourceAsset> _uniqueUrlMappings;
    private readonly FrozenSet<string> _contentSpecificUrls;
    private readonly IReadOnlyList<ResourceAsset> _resources;

    /// <summary>
    /// Initializes a new instance of <see cref="ResourceAssetCollection"/>
    /// </summary>
    /// <param name="resources">The list of resources available.</param>
    public ResourceAssetCollection(IReadOnlyList<ResourceAsset> resources)
    {
        var mappings = new Dictionary<string, ResourceAsset>(StringComparer.OrdinalIgnoreCase);
        var contentSpecificUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _resources = resources;
        foreach (var resource in resources)
        {
            foreach (var property in resource.Properties ?? [])
            {
                if (property.Name.Equals("label", StringComparison.OrdinalIgnoreCase))
                {
                    if (mappings.TryGetValue(property.Value, out var value))
                    {
                        throw new InvalidOperationException($"The static asset '{property.Value}' is already mapped to {value.Url}.");
                    }
                    mappings[property.Value] = resource;
                    contentSpecificUrls.Add(resource.Url);
                }
            }
        }

        _uniqueUrlMappings = mappings.ToFrozenDictionary();
        _contentSpecificUrls = contentSpecificUrls.ToFrozenSet();
    }

    /// <summary>
    /// Gets the unique content-based URL for the specified static asset.
    /// </summary>
    /// <param name="key">The asset name.</param>
    /// <returns>The unique URL if availabe, the same <paramref name="key"/> if not available.</returns>
    public string this[string key] => _uniqueUrlMappings.TryGetValue(key, out var value) ? value.Url : key;

    /// <summary>
    /// Determines whether the specified path is a content-specific URL.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns><c>true</c> if the path is a content-specific URL; otherwise, <c>false</c>.</returns>
    public bool IsContentSpecificUrl(string path) => _contentSpecificUrls.Contains(path);

    // IReadOnlyList<ResourceAsset> implementation
    ResourceAsset IReadOnlyList<ResourceAsset>.this[int index] => _resources[index];
    int IReadOnlyCollection<ResourceAsset>.Count => _resources.Count;
    IEnumerator<ResourceAsset> IEnumerable<ResourceAsset>.GetEnumerator() => _resources.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _resources.GetEnumerator();
}

/// <summary>
/// A resource of the components application, such as a script, stylesheet or image.
/// </summary>
/// <param name="url">The URL of the resource.</param>
/// <param name="properties">The properties associated to this resource.</param>
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public sealed class ResourceAsset(string url, IReadOnlyList<ResourceAssetProperty>? properties)
{
    /// <summary>
    /// Gets the URL that identifies this resource.
    /// </summary>
    public string Url { get; } = url;

    /// <summary>
    /// Gets a list of properties associated to this resource.
    /// </summary>
    public IReadOnlyList<ResourceAssetProperty>? Properties { get; } = properties;

    private string GetDebuggerDisplay() =>
        $"Url: '{Url}' - Properties: {string.Join(", ", Properties?.Select(p => $"{p.Name} = {p.Value}") ?? [])}";
}

/// <summary>
/// A resource property.
/// </summary>
/// <param name="name">The name of the property.</param>
/// <param name="value">The value of the property.</param>
public sealed class ResourceAssetProperty(string name, string value)
{
    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the value of the property.
    /// </summary>
    public string Value { get; } = value;
}

internal static class ResourceCollectionResolver
{
    public static ResourceAssetCollection ResolveResourceCollection(StaticAssetsManifest manifest)
    {
        var descriptors = manifest.Endpoints;
        var resources = new List<ResourceAsset>();

        // We are converting a subset of the descriptors to resources and including a subset of the properties exposed by the
        // descriptors that are useful for the resources in the context of Blazor. Specifically, we pass in the `label` property
        // which contains the human-readable identifier for fingerprinted assets, and the integrity, which can be used to apply
        // subresource integrity to things like images, script tags, etc.
        foreach (var descriptor in descriptors)
        {
            string? label = null;
            string? integrity = null;

            // If there's a selector this means that this is an alternative representation for a resource, so skip it.
            if (descriptor.Selectors.Count == 0)
            {
                var foundProperties = 0;
                for (var i = 0; i < descriptor.Properties.Count; i++)
                {
                    var property = descriptor.Properties[i];
                    if (property.Name.Equals("label", StringComparison.OrdinalIgnoreCase))
                    {
                        label = property.Value;
                        foundProperties++;
                    }

                    else if (property.Name.Equals("integrity", StringComparison.OrdinalIgnoreCase))
                    {
                        integrity = property.Value;
                        foundProperties++;
                    }
                }

                AddResource(resources, descriptor, label, integrity, foundProperties);
            }
        }

        // Sort the resources because we are going to generate a hash for the collection to use when we expose it as an endpoint
        // for webassembly to consume. This way, we can cache this collection forever until it changes.
        resources.Sort((a, b) => string.Compare(a.Url, b.Url, StringComparison.Ordinal));

        var result = new ResourceAssetCollection(resources);
        return result;
    }

    private static void AddResource(
        List<ResourceAsset> resources,
        StaticAssetDescriptor descriptor,
        string? label,
        string? integrity,
        int foundProperties)
    {
        if (label != null || integrity != null)
        {
            var properties = new ResourceAssetProperty[foundProperties];
            var index = 0;
            if (label != null)
            {
                properties[index++] = new ResourceAssetProperty("label", label);
            }
            if (integrity != null)
            {
                properties[index++] = new ResourceAssetProperty("integrity", integrity);
            }

            resources.Add(new ResourceAsset(descriptor.Route, properties));
        }
        else
        {
            resources.Add(new ResourceAsset(descriptor.Route, null));
        }
    }
}
