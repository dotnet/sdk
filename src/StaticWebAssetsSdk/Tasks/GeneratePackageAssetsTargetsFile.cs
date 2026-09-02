// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Xml;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

// Generates a lightweight .targets file that adds a single StaticWebAssetPackageManifest
// item pointing to the JSON manifest file. This replaces the heavyweight XML .props files
// that contained all asset/endpoint data as MSBuild items.
public class GeneratePackageAssetsTargetsFile : Task
{
    [Required]
    public string PackageId { get; set; }

    [Required]
    public string TargetFilePath { get; set; }

    public string PackagePathPrefix { get; set; } = "staticwebassets";

    [Required]
    public string ManifestFileName { get; set; }

    public override bool Execute()
    {
        var normalizedPrefix = PackagePathPrefix.Replace("/", "\\").TrimStart('\\');

        var itemGroup = new XElement("ItemGroup");
        var manifestItem = new XElement("StaticWebAssetPackageManifest",
            new XAttribute("Include", $@"$([System.IO.Path]::GetFullPath('$(MSBuildThisFileDirectory){ManifestFileName}'))"),
            new XElement("SourceId", PackageId),
            new XElement("ContentRoot", $@"$(MSBuildThisFileDirectory)..\{normalizedPrefix}\"),
            new XElement("PackageRoot", @"$([System.IO.Path]::GetFullPath('$(MSBuildThisFileDirectory)..'))"));

        itemGroup.Add(manifestItem);

        var document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
        var root = new XElement("Project", itemGroup);
        document.Add(root);

        var settings = new XmlWriterSettings
        {
            Encoding = Encoding.UTF8,
            CloseOutput = false,
            OmitXmlDeclaration = true,
            Indent = true,
            NewLineOnAttributes = false,
            Async = true
        };

        using var memoryStream = new MemoryStream();
        using (var xmlWriter = XmlWriter.Create(memoryStream, settings))
        {
            document.WriteTo(xmlWriter);
        }

        var data = memoryStream.ToArray();
        this.PersistFileIfChanged(data, TargetFilePath);

        return !Log.HasLoggedErrors;
    }
}
