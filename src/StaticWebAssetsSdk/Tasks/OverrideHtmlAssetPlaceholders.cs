// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public partial class OverrideHtmlAssetPlaceholders : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; } = [];

    [Required]
    public ITaskItem[] Endpoints { get; set; } = [];

    [Required]
    public bool IncludeOnlyHardFingerprintedModules { get; set; }

    [Required]
    public string OutputPath { get; set; } = string.Empty;

    [Required]
    public ITaskItem[] HtmlFiles { get; set; } = [];

    [Output]
    public ITaskItem[] HtmlCandidates { get; set; } = [];

    [Output]
    public ITaskItem[] HtmlFilesToRemove { get; set; } = [];

    [Output]
    public string[] FileWrites { get; set; } = [];

    internal static readonly Regex _assetsRegex = new Regex(@"""(?<fileName>[^""]+)#\[\.{fingerprint}\](?<fileExtension>[^""]+)""");

    internal static readonly Regex _importMapRegex = new Regex(@"<script\s+type=""importmap""\s*>\s*</script>");

    internal static readonly Regex _preloadRegex = new Regex(@"<link\s+rel=""preload""(\s+id=""(?<group>[^""]+)"")?\s*[/]?>");

    public override bool Execute()
    {
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(Endpoints).Where(e => e.AssetFile.EndsWith(".js") || e.AssetFile.EndsWith(".mjs"));
        var resources = CreateResourcesFromEndpoints(endpoints);
        var urlMappings = GroupResourcesByLabel(resources);
        var importMap = CreateImportMapFromResources(resources);

        var htmlFilesToRemove = new List<ITaskItem>();
        var htmlCandidates = new List<ITaskItem>();
        var fileWrites = new List<string>();

        if (!Directory.Exists(OutputPath))
        {
            Directory.CreateDirectory(OutputPath);
        }

        foreach (var item in HtmlFiles)
        {
            if (File.Exists(item.ItemSpec))
            {
                string content = File.ReadAllText(item.ItemSpec);

                // Generate import map
                string outputContent = _importMapRegex.Replace(content, e =>
                {
                    Log.LogMessage("Writing importmap to '{0}'", item.ItemSpec);
                    return $"<script type=\"importmap\">{JsonSerializer.Serialize(importMap, ImportMapSerializerContext.CustomEncoder.Options)}</script>";
                });

                // Generate import map
                outputContent = _preloadRegex.Replace(outputContent, e =>
                {
                    Log.LogMessage("Writing preload links to '{0}'", item.ItemSpec);
                    return GeneratePreloadLinks(resources, e.Groups["group"]?.Value);
                });

                // Fingerprint all assets used in html
                outputContent = _assetsRegex.Replace(outputContent, e =>
                {
                    string assetPath = e.Groups["fileName"].Value + e.Groups["fileExtension"].Value;
                    string fingerprintedAssetPath = GetFingerprintedAssetPath(urlMappings, assetPath);
                    Log.LogMessage("Replacing asset '{0}' with fingerprinted version '{1}'", assetPath, fingerprintedAssetPath);
                    return "\"" + fingerprintedAssetPath + "\"";
                });

                if (content != outputContent)
                {
                    htmlFilesToRemove.Add(item);

                    string outputPath = Path.Combine(OutputPath, FileHasher.HashString(item.ItemSpec) + item.GetMetadata("Extension"));
                    this.PersistFileIfChanged(Encoding.UTF8.GetBytes(outputContent), outputPath);
                    fileWrites.Add(outputPath);

                    var newItem = new TaskItem(outputPath, item.CloneCustomMetadata());
                    newItem.RemoveMetadata("OriginalItemSpec");
                    htmlCandidates.Add(newItem);
                }
            }
        }

        HtmlCandidates = htmlCandidates.ToArray();
        HtmlFilesToRemove = htmlFilesToRemove.ToArray();
        FileWrites = fileWrites.ToArray();
        return true;
    }

    private static string GeneratePreloadLinks(List<ResourceAsset> assets, string? group)
    {
        var links = new List<(int Order, string Value)>();
        foreach (var asset in assets)
        {
            if (asset.PreloadRel == null)
            {
                continue;
            }

            if (group != null && asset.PreloadGroup != group)
            {
                continue;
            }

            var link = new StringBuilder();
            link.Append($"<link href=\"").Append(asset.Url).Append("\" rel=\"").Append(asset.PreloadRel).Append('"');
            if (!string.IsNullOrEmpty(asset.PreloadAs))
            {
                link.Append(" as=\"").Append(asset.PreloadAs).Append('"');
            }
            if (!string.IsNullOrEmpty(asset.PreloadPriority))
            {
                link.Append(" fetchpriority=\"").Append(asset.PreloadPriority).Append('"');
            }
            if (!string.IsNullOrEmpty(asset.PreloadCrossorigin))
            {
                link.Append(" crossorigin=\"").Append(asset.PreloadCrossorigin).Append('"');
            }
            if (!string.IsNullOrEmpty(asset.Integrity))
            {
                link.Append(" integrity=\"").Append(asset.Integrity).Append('"');
            }

            link.Append(" />");
            links.Add((asset.PreloadOrder, link.ToString()));
        }

        links.Sort((a, b) => a.Order.CompareTo(b.Order));
        return String.Join(Environment.NewLine, links.Select(l => l.Value));
    }

    private string GetFingerprintedAssetPath(Dictionary<string, ResourceAsset> urlMappings, string assetPath)
    {
        if (urlMappings.TryGetValue(assetPath, out var asset) && (!IncludeOnlyHardFingerprintedModules || asset.IsHardFingerprinted))
        {
            return asset.Url;
        }

        return assetPath;
    }

    internal List<ResourceAsset> CreateResourcesFromEndpoints(IEnumerable<StaticWebAssetEndpoint> endpoints)
    {
        var resources = new List<ResourceAsset>();

        // We are converting a subset of the descriptors to resources and including a subset of the properties exposed by the
        // descriptors that are useful for the resources in the context of Blazor. Specifically, we pass in the `label` property
        // which contains the human-readable identifier for fingerprinted assets, and the integrity, which can be used to apply
        // subresource integrity to things like images, script tags, etc.
        foreach (var endpoint in endpoints)
        {
            // If there's a selector this means that this is an alternative representation for a resource, so skip it.
            if (endpoint.Selectors?.Length == 0)
            {
                var resourceAsset = new ResourceAsset(endpoint.Route);
                for (var i = 0; i < endpoint.EndpointProperties?.Length; i++)
                {
                    var property = endpoint.EndpointProperties[i];
                    if (property.Name.Equals("label", StringComparison.OrdinalIgnoreCase))
                    {
                        resourceAsset.Label = property.Value;
                    }
                    else if (property.Name.Equals("integrity", StringComparison.OrdinalIgnoreCase))
                    {
                        resourceAsset.Integrity = property.Value;
                    }
                    else if (property.Name.Equals("preloadgroup", StringComparison.OrdinalIgnoreCase))
                    {
                        resourceAsset.PreloadGroup = property.Value;
                    }
                    else if (property.Name.Equals("preloadrel", StringComparison.OrdinalIgnoreCase))
                    {
                        resourceAsset.PreloadRel = property.Value;
                    }
                    else if (property.Name.Equals("preloadas", StringComparison.OrdinalIgnoreCase))
                    {
                        resourceAsset.PreloadAs = property.Value;
                    }
                    else if (property.Name.Equals("preloadpriority", StringComparison.OrdinalIgnoreCase))
                    {
                        resourceAsset.PreloadPriority = property.Value;
                    }
                    else if (property.Name.Equals("preloadcrossorigin", StringComparison.OrdinalIgnoreCase))
                    {
                        resourceAsset.PreloadCrossorigin = property.Value;
                    }
                    else if (property.Name.Equals("preloadorder", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!int.TryParse(property.Value, out int order))
                        {
                            order = 0;
                        }

                        resourceAsset.PreloadOrder = order;
                    }
                }

                var asset = Assets.FirstOrDefault(a => a.ItemSpec == endpoint.AssetFile);
                if (asset != null)
                {
                    resourceAsset.IsHardFingerprinted = asset.GetMetadata("RelativePath").Contains("#[.{fingerprint}]!");
                }

                resources.Add(resourceAsset);
            }
        }

        return resources;
    }

    private ImportMap CreateImportMapFromResources(List<ResourceAsset> assets)
    {
        Dictionary<string, string>? imports = new();
        Dictionary<string, Dictionary<string, string>>? scopes = new();
        Dictionary<string, string>? integrity = new();

        foreach (var asset in assets)
        {
            if (IncludeOnlyHardFingerprintedModules && !asset.IsHardFingerprinted)
            {
                continue;
            }

            if (asset.Integrity != null)
            {
                integrity ??= [];
                integrity[$"./{asset.Url}"] = asset.Integrity;
            }

            // Only fingerprinted assets have label
            if (asset.Label != null)
            {
                imports ??= [];
                imports[$"./{asset.Label}"] = $"./{asset.Url}";
            }
        }

        return new ImportMap(imports, scopes, integrity);
    }

    private static Dictionary<string, ResourceAsset> GroupResourcesByLabel(List<ResourceAsset> resources)
    {
        var mappings = new Dictionary<string, ResourceAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in resources)
        {
            if (resource.Label != null)
            {
                if (mappings.TryGetValue(resource.Label, out var value))
                {
                    throw new InvalidOperationException($"The static asset '{resource.Label}' is already mapped to {value.Url}.");
                }
                mappings[resource.Label] = resource;
            }
        }

        return mappings;
    }
}

internal sealed class ResourceAsset(string url)
{
    public string Url { get; } = url;
    public string? Label { get; set; }
    public string? Integrity { get; set; }
    public string? PreloadGroup { get; set; }
    public string? PreloadRel { get; set; }
    public string? PreloadAs { get; set; }
    public string? PreloadPriority { get; set; }
    public string? PreloadCrossorigin { get; set; }
    public int PreloadOrder { get; set; }
    public bool IsHardFingerprinted { get; set; } = true;
}

internal class ImportMap(Dictionary<string, string> imports, Dictionary<string, Dictionary<string, string>> scopes, Dictionary<string, string> integrity)
{
    public Dictionary<string, string> Imports { get; set; } = imports;
    public Dictionary<string, Dictionary<string, string>> Scopes { get; set; } = scopes;
    public Dictionary<string, string> Integrity { get; set; } = integrity;
}

[JsonSerializable(typeof(ImportMap))]
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
