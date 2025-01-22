// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.StaticWebAssets;

public class WriteImportMapToHtml : Task
{
    [Required]
    public string ManifestPath { get; set; } = string.Empty;

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

    [SuppressMessage("Performance", "SYSLIB1045:Convert to 'GeneratedRegexAttribute'.", Justification = "The assembly targets .NET framework as well")]
    private static readonly Regex _assetsRegex = new Regex(@"@Assets\['([^']*)'\]");

    [SuppressMessage("Performance", "SYSLIB1045:Convert to 'GeneratedRegexAttribute'.", Justification = "The assembly targets .NET framework as well")]
    private static readonly Regex _importMapRegex = new Regex(@"<!--\s*ImportMap\s*-->");

    public override bool Execute()
    {
        var manifest = StaticAssetsManifest.Parse(ManifestPath);
        var resources = ResourceCollectionResolver.ResolveResourceCollection(manifest);
        var importMap = ImportMapDefinition.FromResourceCollection(resources);

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
                    return $"<script type='importmap'>{importMap.ToJson()}</script>";
                });

                // Fingerprint all assets used in html
                outputContent = _assetsRegex.Replace(outputContent, e =>
                {
                    string assetPath = e.Groups[1].Value;
                    string fingerprintedAssetPath = resources[assetPath];
                    Log.LogMessage("Replacing asset '{0}' with fingerprinted version '{1}'", assetPath, fingerprintedAssetPath);
                    return fingerprintedAssetPath;
                });

                if (content != outputContent)
                {
                    string outputPath = Path.Combine(OutputPath, Path.GetRandomFileName() + item.GetMetadata("Extension"));
                    File.WriteAllText(outputPath, outputContent);
                    htmlFilesToRemove.Add(item);

                    var htmlItem = new TaskItem(outputPath, item.CloneCustomMetadata());
                    htmlCandidates.Add(htmlItem);
                    fileWrites.Add(outputPath);
                }
            }
        }

        HtmlCandidates = htmlCandidates.ToArray();
        HtmlFilesToRemove = htmlFilesToRemove.ToArray();
        FileWrites = fileWrites.ToArray();
        return true;
    }
}
