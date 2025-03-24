// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public partial class WriteImportMapToHtml : Task
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

    // "([^"]+)(#\[\.{fingerprint}\])([^"]+)"
    // 1.group = file name
    // 2.group = fingerprint placeholder
    // 3.group = file extension
    // wrapped in quotes
    private static readonly Regex _assetsRegex = new Regex(@"""([^""]+)(#\[\.{fingerprint}\])([^""]+)""");

    private static readonly Regex _importMapRegex = new Regex(@"<script\s+type=""importmap""\s*>\s*</script>");

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

                // Fingerprint all assets used in html
                outputContent = _assetsRegex.Replace(outputContent, e =>
                {
                    string assetPath = e.Groups[1].Value + e.Groups[3].Value;
                    string fingerprintedAssetPath = GetFingerprintedAssetPath(urlMappings, assetPath);
                    Log.LogMessage("Replacing asset '{0}' with fingerprinted version '{1}'", assetPath, fingerprintedAssetPath);
                    return "\"" + fingerprintedAssetPath + "\"";
                });

                if (content != outputContent)
                {
                    htmlFilesToRemove.Add(item);

                    string outputPath = Path.Combine(OutputPath, FileHasher.HashString(item.ItemSpec) + item.GetMetadata("Extension"));
                    if (this.PersistFileIfChanged(Encoding.UTF8.GetBytes(outputContent), outputPath))
                    {
                        fileWrites.Add(outputPath);
                    }

                    htmlCandidates.Add(new TaskItem(outputPath, item.CloneCustomMetadata()));
                }
            }
        }

        HtmlCandidates = htmlCandidates.ToArray();
        HtmlFilesToRemove = htmlFilesToRemove.ToArray();
        FileWrites = fileWrites.ToArray();
        return true;
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
            string? label = null;
            string? integrity = null;

            // If there's a selector this means that this is an alternative representation for a resource, so skip it.
            if (endpoint.Selectors?.Length == 0)
            {
                for (var i = 0; i < endpoint.EndpointProperties?.Length; i++)
                {
                    var property = endpoint.EndpointProperties[i];
                    if (property.Name.Equals("label", StringComparison.OrdinalIgnoreCase))
                    {
                        label = property.Value;
                    }
                    else if (property.Name.Equals("integrity", StringComparison.OrdinalIgnoreCase))
                    {
                        integrity = property.Value;
                    }
                }

                bool isHardFingerprinted = true;
                var asset = Assets.FirstOrDefault(a => a.ItemSpec == endpoint.AssetFile);
                if (asset != null)
                {
                    isHardFingerprinted = asset.GetMetadata("RelativePath").Contains("#[.{fingerprint}]!");
                }

                resources.Add(new ResourceAsset(endpoint.Route, label, integrity, isHardFingerprinted));
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

internal sealed class ResourceAsset(string url, string? label, string? integrity, bool isHardFingerprinted)
{
    public string Url { get; } = url;
    public string? Label { get; set; } = label;
    public string? Integrity { get; set; } = integrity;
    public bool IsHardFingerprinted { get; set; } = isHardFingerprinted;
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
